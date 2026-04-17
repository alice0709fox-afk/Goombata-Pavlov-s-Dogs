using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
   [SerializeField] private float speedRun = 500;
   [SerializeField] private float speedWalk = 300;
   [SerializeField] private float jumpForce = 30;
   [SerializeField, Range(1, 2)] private int maxJumpCount = 2;
   [SerializeField] private GroundChecker groundChecker;

   private bool _wasGrounded;
   private int _currentJumpCount;
   private PlayerInputHandler _input; 
   private Rigidbody2D _rigidbody;

   
   public Vector2 MoveInput { get; private set; }
   public bool JumpPressed { get; private set; }
   public bool RunningPressed { get; private set; }

   private void Start()
   {
      _rigidbody = GetComponent<Rigidbody2D>();
      _input =  GetComponent<PlayerInputHandler>();
      maxJumpCount = Mathf.Clamp(maxJumpCount, 1, 2);

      _input.OnMove += SetMove;
      _input.OnJumpPressed += SetJump;
      _input.OnRunningPressed += SetRunning;

      if (groundChecker != null)
      {
         _wasGrounded = groundChecker.CheckGround();
      }
   }

   private void OnDestroy()
   {
      _input.OnMove -= SetMove;
      _input.OnJumpPressed -= SetJump;
      _input.OnRunningPressed -= SetRunning;
   }

   private void FixedUpdate()
   {
      Move();
   }

   private void Update()
   {
      Jump();
      ResetJumpCount();
   }

   private void Move()
   {
      var inputX = MoveInput.x * GetSpeed() * Time.fixedDeltaTime;
      SetVelocity(inputX, _rigidbody.linearVelocity.y);
   }

   private void Jump()
   {
      if (!JumpPressed) return;
      if (_currentJumpCount >= maxJumpCount) return;
      
      SetVelocity(_rigidbody.linearVelocity.x, jumpForce);
      _currentJumpCount++;
      
      JumpPressed = false;
   }

   private void ResetJumpCount()
   {
      bool isGrounded = groundChecker.CheckGround();

      if (isGrounded)
         _currentJumpCount = 0;

      _wasGrounded =  isGrounded;
   }
   
   private void SetVelocity(float x, float y)
      => _rigidbody.linearVelocity = new Vector2(x, y);

   private float GetSpeed() => RunningPressed ? speedRun : speedWalk;
   
   private void SetMove(Vector2 value) => MoveInput = value;
   private void SetJump() => JumpPressed = true;
   private void SetRunning(bool value) => RunningPressed = value;
}
