using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;  // Import the Input System namespace

namespace Fragsurf.Movement
{
    [AddComponentMenu("Fragsurf/Surf Character")]
    public class SurfCharacter : MonoBehaviour, ISurfControllable
    {
        public enum ColliderType
        {
            Capsule,
            Box
        }

        [Header("Physics Settings")]
        public Vector3 colliderSize = new Vector3(1f, 2f, 1f);
        [HideInInspector] public ColliderType collisionType { get { return ColliderType.Box; } }
        public float weight = 75f;
        public float rigidbodyPushForce = 2f;
        public bool solidCollider = false;

        [Header("View Settings")]
        public Transform viewTransform;
        public Transform playerRotationTransform;

        [Header("Crouching setup")]
        public float crouchingHeightMultiplier = 0.5f;
        public float crouchingSpeed = 10f;
        float defaultHeight;
        bool allowCrouch = true;

        [Header("Features")]
        public bool crouchingEnabled = true;
        public bool slidingEnabled = false;
        public bool laddersEnabled = true;
        public bool supportAngledLadders = true;

        [Header("Step offset")]
        public bool useStepOffset = false;
        public float stepOffset = 0.35f;

        [Header("Movement Config")]
        [SerializeField] public MovementConfig movementConfig;

        private GameObject _groundObject;
        private Vector3 _baseVelocity;
        private Collider _collider;
        private Vector3 _angles;
        private Vector3 _startPosition;
        private GameObject _colliderObject;
        private GameObject _cameraWaterCheckObject;
        private CameraWaterCheck _cameraWaterCheck;

        private MoveData _moveData = new MoveData();
        private SurfController _controller = new SurfController();

        private Rigidbody rb;

        private List<Collider> triggers = new List<Collider>();
        private int numberOfTriggers = 0;

        private bool underwater = false;

        ///// Properties /////
        public MoveType moveType { get { return MoveType.Walk; } }
        public MovementConfig moveConfig { get { return movementConfig; } }
        public MoveData moveData { get { return _moveData; } }
        public new Collider collider { get { return _collider; } }

        public GameObject groundObject { get { return _groundObject; } set { _groundObject = value; } }
        public Vector3 baseVelocity { get { return _baseVelocity; } }
        public Vector3 forward { get { return viewTransform.forward; } }
        public Vector3 right { get { return viewTransform.right; } }
        public Vector3 up { get { return viewTransform.up; } }

        Vector3 prevPosition;

        // Input Action references
        private PlayerInput playerInput;
        public SourceGrabber grabber;

        private void OnEnable()
        {
            // Subscribe to the actions from the PlayerInput component
            playerInput.actions["Move"].performed += OnMove;
            playerInput.actions["Jump"].performed += OnJump;
            playerInput.actions["Grab"].performed += grabber.OnGrab;
            playerInput.actions["Throw"].performed += grabber.OnThrow;
        }

        private void OnDisable()
        {
            // Unsubscribe from actions
            playerInput.actions["Move"].performed -= OnMove;
            playerInput.actions["Jump"].performed -= OnJump;
            playerInput.actions["Grab"].performed -= grabber.OnGrab;
            playerInput.actions["Throw"].performed -= grabber.OnThrow;
        }

        private void Awake()
        {
            _controller.playerTransform = playerRotationTransform;

            if (viewTransform != null)
            {
                _controller.camera = viewTransform;
                _controller.cameraYPos = viewTransform.localPosition.y;
            }

            // Initialize input actions
            playerInput = FindFirstObjectByType<PlayerInput>();
            
        }

        private void Start()
        {
            _colliderObject = new GameObject("PlayerCollider");
            _colliderObject.layer = gameObject.layer;
            _colliderObject.transform.SetParent(transform);
            _colliderObject.transform.rotation = Quaternion.identity;
            _colliderObject.transform.localPosition = Vector3.zero;
            _colliderObject.transform.SetSiblingIndex(0);

            // Water check setup
            _cameraWaterCheckObject = new GameObject("Camera water check");
            _cameraWaterCheckObject.layer = gameObject.layer;
            _cameraWaterCheckObject.transform.position = viewTransform.position;

            SphereCollider _cameraWaterCheckSphere = _cameraWaterCheckObject.AddComponent<SphereCollider>();
            _cameraWaterCheckSphere.radius = 0.1f;
            _cameraWaterCheckSphere.isTrigger = true;

            Rigidbody _cameraWaterCheckRb = _cameraWaterCheckObject.AddComponent<Rigidbody>();
            _cameraWaterCheckRb.useGravity = false;
            _cameraWaterCheckRb.isKinematic = true;

            _cameraWaterCheck = _cameraWaterCheckObject.AddComponent<CameraWaterCheck>();

            prevPosition = transform.position;

            if (viewTransform == null)
                viewTransform = Camera.main.transform;

            if (playerRotationTransform == null && transform.childCount > 0)
                playerRotationTransform = transform.GetChild(0);

            _collider = gameObject.GetComponent<Collider>();

            if (_collider != null)
                GameObject.Destroy(_collider);

            rb = gameObject.GetComponent<Rigidbody>();
            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody>();

            allowCrouch = crouchingEnabled;

            rb.isKinematic = false;
            rb.useGravity = true;
            rb.angularDamping = 0f;
            rb.linearDamping = 0f;
            rb.mass = weight;

            switch (collisionType)
            {
                case ColliderType.Box:
                    _collider = _colliderObject.AddComponent<BoxCollider>();
                    var boxc = (BoxCollider)_collider;
                    boxc.size = colliderSize;
                    defaultHeight = boxc.size.y;
                    break;
                case ColliderType.Capsule:
                    _collider = _colliderObject.AddComponent<CapsuleCollider>();
                    var capc = (CapsuleCollider)_collider;
                    capc.height = colliderSize.y;
                    capc.radius = colliderSize.x / 2f;
                    defaultHeight = capc.height;
                    break;
            }

            _moveData.slopeLimit = movementConfig.slopeLimit;
            _moveData.rigidbodyPushForce = rigidbodyPushForce;
            _moveData.slidingEnabled = slidingEnabled;
            _moveData.laddersEnabled = laddersEnabled;
            _moveData.angledLaddersEnabled = supportAngledLadders;
            _moveData.playerTransform = transform;
            _moveData.viewTransform = viewTransform;
            _moveData.viewTransformDefaultLocalPos = viewTransform.localPosition;
            _moveData.defaultHeight = defaultHeight;
            _moveData.crouchingHeight = crouchingHeightMultiplier;
            _moveData.crouchingSpeed = crouchingSpeed;
            _collider.isTrigger = !solidCollider;
            _moveData.origin = transform.position;
            _startPosition = transform.position;
            _moveData.useStepOffset = useStepOffset;
            _moveData.stepOffset = stepOffset;
        }

        private void Update()
        {
            _colliderObject.transform.rotation = Quaternion.identity;
            UpdateMoveData();

            // Previous movement code
            Vector3 positionalMovement = transform.position - prevPosition;
            transform.position = prevPosition;
            moveData.origin += positionalMovement;

            // Update triggers
            if (numberOfTriggers != triggers.Count)
            {
                numberOfTriggers = triggers.Count;

                underwater = false;
                triggers.RemoveAll(item => item == null);
                foreach (Collider trigger in triggers)
                {
                    if (trigger == null)
                        continue;

                    if (trigger.GetComponentInParent<Water>())
                        underwater = true;
                }
            }

            _moveData.cameraUnderwater = _cameraWaterCheck.IsUnderwater();
            _cameraWaterCheckObject.transform.position = viewTransform.position;
            moveData.underwater = underwater;

            if (allowCrouch)
                _controller.Crouch(this, movementConfig, Time.deltaTime);

            _controller.ProcessMovement(this, movementConfig, Time.deltaTime);

            transform.position = moveData.origin;
            prevPosition = transform.position;

            _colliderObject.transform.rotation = Quaternion.identity;
        }

        private void UpdateMoveData()
        {
            // Use PlayerInput's action values directly for movement
            Vector2 moveInput = playerInput.actions["Move"].ReadValue<Vector2>();
            _moveData.verticalAxis = moveInput.y;
            _moveData.horizontalAxis = moveInput.x;

            _moveData.sprinting = moveInput.magnitude > 0.5f;  // For sprinting

            _moveData.crouching = playerInput.actions["Crouch"].ReadValue<float>() > 0.5f;

            bool moveLeft = _moveData.horizontalAxis < 0f;
            bool moveRight = _moveData.horizontalAxis > 0f;
            bool moveFwd = _moveData.verticalAxis > 0f;
            bool moveBack = _moveData.verticalAxis < 0f;

            if (!moveLeft && !moveRight)
                _moveData.sideMove = 0f;
            else if (moveLeft)
                _moveData.sideMove = -moveConfig.acceleration;
            else if (moveRight)
                _moveData.sideMove = moveConfig.acceleration;

            if (!moveFwd && !moveBack)
                _moveData.forwardMove = 0f;
            else if (moveFwd)
                _moveData.forwardMove = moveConfig.acceleration;
            else if (moveBack)
                _moveData.forwardMove = -moveConfig.acceleration;

            _moveData.viewAngles = _angles;
        }

        // Event handlers for the input actions
        private void OnMove(InputAction.CallbackContext context)
        {
            Vector2 moveInput = context.ReadValue<Vector2>();
            _moveData.horizontalAxis = moveInput.x;
            _moveData.verticalAxis = moveInput.y;
        }

        private void OnJump(InputAction.CallbackContext context)
        {
            // Handle jump input
            _moveData.wishJump = true;
        }
    }
}
