using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    private Rigidbody2D rb;
    private Animator anim; 
    public float moveSpeed = 5f;
    public float runSpeed = 8f;
    public int flip = 1;
    public float lastHorizontal = 0;

    public static PlayerMovement instance;
    public bool InputLocked { get; set; }
    public Vector2 TouchMove { get; set; }


    private void Awake()
    {
        instance = this;
    }
    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
    }

    private void Update()
    {
    }

    void FixedUpdate()
    {
        bool locked = DebugUnlockPanel.BlocksGameplayInput || InputLocked || (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive);
        float horizontal = locked ? 0f : Input.GetAxisRaw("Horizontal");
        float vertical = locked ? 0f : Input.GetAxisRaw("Vertical");
        if (!locked && TouchMove.sqrMagnitude > 0) { horizontal = TouchMove.x; vertical = TouchMove.y; }
        Vector2 inputDirection = new Vector2(horizontal, vertical);
        if (inputDirection.magnitude > 1)
        {
            inputDirection = inputDirection.normalized;
        }
        if (horizontal != 0)
        {
            lastHorizontal = horizontal;
            if (horizontal < 0)
            {
                flip = -1;
            }
            else
            {
                flip = 1;
            }
        }
        else if (horizontal == 0 && vertical == 0)
        {
            // 停止移动时保持最后一次的朝向
            if (lastHorizontal < 0)
            {
                flip = -1;
            }
            else if (lastHorizontal > 0)
            {
                flip = 1;
            }
        }
        transform.localScale = new Vector3(flip, 1, 1);

        bool isRunning = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        float currentSpeed = isRunning ? runSpeed : moveSpeed;
        // 先保证物理移动生效，再更新动画——动画报错绝不能阻断移动。
        rb.velocity = inputDirection * currentSpeed;
        UpdateAnimator(horizontal, vertical, isRunning);
    }

    private int animParamState = 0; // 0=未检查 1=参数齐全 2=缺参数或无控制器
    private void OnDisable() { TouchMove = Vector2.zero; }
    private void OnApplicationPause(bool paused) { if (paused) TouchMove = Vector2.zero; }

    private void UpdateAnimator(float horizontal, float vertical, bool isRunning)
    {
        if (anim == null || anim.runtimeAnimatorController == null) return;
        if (animParamState == 0)
        {
            animParamState = 2;
            bool hasH = false, hasV = false, hasRun = false;
            foreach (AnimatorControllerParameter p in anim.parameters)
            {
                if (p.name == "horizontal") hasH = true;
                else if (p.name == "vertical") hasV = true;
                else if (p.name == "IsRun") hasRun = true;
            }
            if (hasH && hasV && hasRun) animParamState = 1;
        }
        // 只对控制器中真实存在的参数赋值，避免每帧刷 "parameter does not exist"。
        if (animParamState == 1)
        {
            anim.SetFloat("horizontal", Mathf.Abs(horizontal));
            anim.SetFloat("vertical", Mathf.Abs(vertical));
            anim.SetBool("IsRun", isRunning);
        }
    }
}





