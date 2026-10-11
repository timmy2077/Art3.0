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
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical"); 
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
        anim.SetFloat("horizontal", Mathf.Abs(horizontal));
        anim.SetFloat("vertical", Mathf.Abs(vertical));

        bool isRunning = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        float currentSpeed = isRunning ? runSpeed : moveSpeed;
        rb.velocity = inputDirection * currentSpeed;
        anim.SetBool("IsRun", isRunning);
        }
    }  





