using UnityEngine;

namespace DVPets
{
    public class PetController : MonoBehaviour
    {
        public Mode ControlMode { get; set; }

        private Animator _animator;
        private static readonly int _moveParam = Animator.StringToHash("Move");
        private static readonly int _speedParam = Animator.StringToHash("Speed");

        private State _state = State.Idle;
        private float _idleCountdown;
        private Vector3? _targetPosition;

        private float _movementBlock;
        private float _currentSpeed;
        private float _speedSmoothing;
        private float _yawSmoothing;
        private bool _sprinting;

        private Transform _frontFootOrigin;
        private Transform _rearFootOrigin;

        private void Start()
        {
            _animator = GetComponent<Animator>();
            _idleCountdown = GetNewIdleDuration();
            _raycastMask = LayerMask.GetMask("Terrain", "Default");
            _petCollider = GetComponentInChildren<Collider>();

            _frontFootOrigin = new GameObject("[front foot]").transform;
            _frontFootOrigin.SetParent(transform);
            _frontFootOrigin.localPosition = new Vector3(0, SURFACE_RAYCAST_HEIGHT, CAT_FOOT_OFFSET);

            _rearFootOrigin = new GameObject("[rear foot]").transform;
            _rearFootOrigin.SetParent(transform);
            _rearFootOrigin.localPosition = new Vector3(0, SURFACE_RAYCAST_HEIGHT, -CAT_FOOT_OFFSET);
        }

        private static float GetNewIdleDuration() => Random.Range(MIN_IDLE_TIME, MAX_IDLE_TIME);

        private void PrepareForMove()
        {
            if (!(_currentSpeed > 0 || _movementBlock > 0))
            {
                var currentClip = _animator.GetCurrentAnimatorClipInfo(0)[0];
                var currentState = _animator.GetCurrentAnimatorStateInfo(0);
                _movementBlock = currentClip.clip.length - (currentState.normalizedTime % 1);// - Time.deltaTime;
            }
        }

        private void SetMovementSpeed(float speed)
        {
            _currentSpeed = speed;

            if (speed == 0)
            {
                _speedSmoothing = 0;
                _yawSmoothing = 0;
                _animator.SetBool(_moveParam, false);
                _animator.SetFloat(_speedParam, 1);
            }
            else
            {
                _animator.SetBool(_moveParam, true);
                _animator.SetFloat(_speedParam, Mathf.Max(0.1f, speed * MOVE_TO_ANIM_SPEED_SCALE));
            }
        }

        private void Update()
        {
            bool tooFar = IsTooFarFromPlayer();

            if (tooFar)
            {
                _targetPosition = PlayerManager.PlayerTransform.position;

                if (_state != State.Following)
                {
                    _state = State.Following;
                    PrepareForMove();
                    return;
                }
            }
            else if (!tooFar && (_state == State.Following))
            {
                ChooseNewWanderTarget();
                return;
            }

            if (_state == State.Idle)
            {
                _idleCountdown -= Time.deltaTime;
                if (_idleCountdown <= 0)
                {
                    ChooseNewWanderTarget();
                    return;
                }
            }
            else if (_targetPosition != null)
            {
                float targetSqrDistance = (transform.position - _targetPosition.Value).sqrMagnitude;
                if (targetSqrDistance <= TARGET_SQR_RADIUS)
                {
                    _state = State.Idle;
                    _targetPosition = null;
                    _idleCountdown = GetNewIdleDuration();

                    SetMovementSpeed(0);
                    return;
                }
            }
            
            if (_targetPosition.HasValue)
            {
                if (_movementBlock > 0)
                {
                    _movementBlock -= Time.deltaTime;

                    if (_movementBlock > 0) return;
                }

                // follow or move to point
                Vector3 targetRay = Vector3.ProjectOnPlane(_targetPosition.Value - transform.position, Vector3.up);

                if (targetRay.sqrMagnitude > SPRINT_SQR_DISTANCE)
                {
                    _sprinting = true;
                }
                else if (targetRay.sqrMagnitude < MAX_PLAYER_SQR_DISTANCE)
                {
                    _sprinting = false;
                }

                float targetSpeed = _sprinting ? SPRINT_SPEED : MOVE_SPEED;
                float newSpeed = Mathf.SmoothDamp(_currentSpeed, targetSpeed, ref _speedSmoothing, Time.deltaTime * SPEED_SMOOTHING);
                SetMovementSpeed(newSpeed);

                //float targetYaw = Quaternion.LookRotation(targetRay, Vector3.up).eulerAngles.y - 180;
                //float newYaw = Mathf.SmoothDamp(transform.rotation.eulerAngles.y - 180, targetYaw, ref _yawSmoothing, Time.deltaTime * YAW_SMOOTHING);
                //transform.rotation = Quaternion.Euler(0, newYaw + 180, 0);

                Quaternion targetRot = Quaternion.LookRotation(targetRay, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, PetsMain.Settings.MaxRotateSpeed);

                Vector3 newPosition;
                if (targetRay.sqrMagnitude < Mathf.Pow(newSpeed * Time.deltaTime, 2))
                {
                    newPosition = _targetPosition.Value;
                }
                else
                {
                    newPosition = transform.position + transform.forward * newSpeed * Time.deltaTime;
                }

                transform.position = newPosition;
                AlignToSurface();
            }
        }

        private bool IsTooFarFromPlayer()
        {
            if (ControlMode == Mode.CarWander) return false;

            Vector3 offset = Vector3.ProjectOnPlane(PlayerManager.PlayerTransform.position - transform.position, Vector3.up);
            return offset.sqrMagnitude > MAX_PLAYER_SQR_DISTANCE;
        }

        private void ChooseNewWanderTarget()
        {
            _state = State.MoveTowardPoint;

            PrepareForMove();

            if (ControlMode == Mode.GroundFollow)
            {
                float angle = Random.value * 360;
                float distance = Random.Range(PERSONAL_SPACE, MAX_PLAYER_DISTANCE);
                Vector3 playerRay = Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward * distance;

                _targetPosition = PlayerManager.PlayerTransform.TransformPoint(playerRay);

                // check for object in the way
                Vector3 targetRay = _targetPosition.Value - transform.position;
                float moveDistance = targetRay.magnitude;
                Vector3 castOrigin = transform.position + Vector3.up * OBSTACLE_RAYCAST_HEIGHT;
                RaycastHit? hit = GetRaycastHit(castOrigin, targetRay, moveDistance);

                if (hit.HasValue)
                {
                    moveDistance = hit.Value.distance - PERSONAL_SPACE;
                    _targetPosition = transform.position + targetRay.normalized * moveDistance;
                }
            }
            else
            {
                // TODO
            }
        }

        private Collider _petCollider;

        private int _raycastMask;
        private static readonly RaycastHit[] _raycastResults = new RaycastHit[16];

        private void AlignToSurface()
        {
            var frontHit = GetRaycastHit(_frontFootOrigin.position, Vector3.down, 10);
            var rearHit = GetRaycastHit(_rearFootOrigin.position, Vector3.down, 10);

            transform.position = (frontHit.Value.point + rearHit.Value.point) / 2;

            Vector3 lookDir = frontHit.Value.point - rearHit.Value.point;
            Quaternion upVectorRot = Quaternion.AngleAxis(-90, transform.right);
            Quaternion targetRot = Quaternion.LookRotation(lookDir, upVectorRot * lookDir);

            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, PetsMain.Settings.MaxRotateSpeed);
        }

        private RaycastHit? GetRaycastHit(Vector3 origin, Vector3 direction, float maxDistance)
        {
            int hitCount = Physics.RaycastNonAlloc(origin, direction, _raycastResults, maxDistance, _raycastMask, QueryTriggerInteraction.Ignore);

            float collisionDistance = float.PositiveInfinity;
            RaycastHit? hit = null;

            for (int i = 0; i < hitCount; i++)
            {
                var result = _raycastResults[i];
                if ((result.distance < collisionDistance) && (result.collider != _petCollider))
                {
                    collisionDistance = result.distance;
                    hit = result;
                }
            }

            return hit;
        }

        private const float PERSONAL_SPACE = 0.5f;
        public const float MAX_PLAYER_DISTANCE = 20;
        public const float MAX_PLAYER_SQR_DISTANCE = MAX_PLAYER_DISTANCE * MAX_PLAYER_DISTANCE;
        private const float TARGET_SQR_RADIUS = 0.1f;

        private const float SPRINT_SQR_DISTANCE = MAX_PLAYER_SQR_DISTANCE * 4;

        private const float MIN_IDLE_TIME = 1;
        private const float MAX_IDLE_TIME = 20;

        private const float MOVE_SPEED = 1.6f;
        private const float SPRINT_SPEED = 4f;
        private const float MOVE_TO_ANIM_SPEED_SCALE = 0.75f;
        private const float SPEED_SMOOTHING = 25;
        private const float YAW_SMOOTHING = 30;

        private const float SURFACE_RAYCAST_HEIGHT = 1;
        private const float OBSTACLE_RAYCAST_HEIGHT = 0.5f;

        private const float CAT_FOOT_OFFSET = 0.15f;

        public enum Mode
        {
            GroundFollow,
            CarWander,
        }

        private enum State
        {
            Idle,
            Following,
            MoveTowardPoint,
        }
    }
}