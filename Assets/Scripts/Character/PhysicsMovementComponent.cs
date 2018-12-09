﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(CharacterInfo))]
public class PhysicsMovementComponent : MonoBehaviour {

    public delegate void EnterGrounding();
    public delegate void ExitGrounding();
    public delegate void EnterJumping();
    public delegate void ExitJumping();
    public delegate void EnterDashing();
    public delegate void ExitDashing();
    public delegate void EnterSlamming();
    public delegate void ExitSlamming();

    public static event EnterGrounding OnEnterGrounding;
    public static event ExitGrounding OnExitGrounding;
    public static event EnterJumping OnEnterJumping;
    public static event ExitJumping OnExitJumping;
    public static event EnterDashing OnEnterDashing;
    public static event ExitDashing OnExitDashing;
    public static event EnterSlamming OnEnterSlamming;
    public static event ExitSlamming OnExitSlamming;



    public float speedForwardMin = .5f;
    public float speedRightMin = .5f;
    public float maxSpeed = 3;
    public float speedJump = 8;
    public float speedDash = 10;
    public float dashPush = 2;
    public float dashCoolDown = .5f;
    public float speedSlam = 20;
    public float slamRadius = 20;
    public float slamHeightLimit = 2;
    public float slamImpactSpeedMax = 100;
    public float airControlForward = .5f;
    public float airControlRight = .5f;

    public float attackMod = 1;

    public float groundCheckPercent = 1.1f;

    public LayerMask layerMask;

    private bool shouldJump = false, shouldDash = false;
    private bool shouldSlam = false;
    private float verticleAxisValue, horizontalAxisValue;
    private Vector3 velocity = Vector3.zero;

    private MovementState state = MovementState.Jumping;

    private bool haveJumpDashed = false;
    private float dashTimer = 0;

    void Start () {
		
	}
	
	void Update () {
    }

    private void FixedUpdate()
    {
        switch (state)
        {
            case MovementState.Grounding:
                MoveForward(verticleAxisValue);
                MoveRight(horizontalAxisValue);
                if (!IsMovingOnGround())
                {
                    state = MovementState.Jumping;
                }
                if (shouldJump)
                {
                    Jump();
                    state = MovementState.Jumping;
                }
                if (shouldDash)
                {
                    Dash();
                    state = MovementState.Dashing;
                }
                ClearRequests();
                break;
            case MovementState.Jumping:
                MoveForward(airControlForward * verticleAxisValue);
                MoveRight(airControlRight * horizontalAxisValue);
                if (IsMovingOnGround())
                {
                    haveJumpDashed = false;
                    state = MovementState.Grounding;
                }
                if (shouldSlam)
                {
                    Slam();
                    state = MovementState.Slamming;
                }
                if (shouldDash && !haveJumpDashed)
                {
                    Dash();
                    haveJumpDashed = true;
                    state = MovementState.Dashing;
                }
                ClearRequests();
                break;
            case MovementState.Dashing:
                dashTimer += Time.fixedDeltaTime;
                if(dashTimer > dashCoolDown)
                {
                    dashTimer = 0;
                    state = MovementState.Jumping;
                }
                foreach(Collider col in GetDashedColliders())
                {
                    if(col.gameObject == gameObject) { continue; }
                    col.gameObject.GetComponent<Rigidbody>().AddForce(ComputeVectorSelfBottomToObj(col.gameObject).normalized * dashPush * GetComponent<Rigidbody>().velocity.magnitude * ComputePushModifier(col.gameObject), ForceMode.VelocityChange);
                    col.gameObject.GetComponent<CharacterInfo>().ragePercent += ComputeDamageToApply(dashPush * GetComponent<Rigidbody>().velocity.magnitude);
                }
                ClearRequests();
                break;
            case MovementState.Slamming:
                if (IsMovingOnGround())
                {
                    Collider[] outAffectedPlayers = new Collider[4];
                    int affectedPlayersNum = Physics.OverlapSphereNonAlloc(transform.position, slamRadius, outAffectedPlayers, layerMask);
                    for (int i = 0; i < affectedPlayersNum; i++)
                    {
                        if (outAffectedPlayers[i].gameObject == gameObject) { continue; }
                        else if (outAffectedPlayers[i].GetComponent<Rigidbody>() != null)
                        {
                            if(outAffectedPlayers[i].GetComponent<Renderer>().bounds.center.y > GetLowestPoint().y + slamHeightLimit) { continue; }
                            Vector3 selfToOther = outAffectedPlayers[i].GetComponent<Renderer>().bounds.center - GetLowestPoint();
                            Vector3 slamImpactVelocity = selfToOther.normalized * slamImpactSpeedMax * Mathf.Lerp(0, 1, (slamRadius - selfToOther.magnitude) / slamRadius);
                            Debug.Log(slamImpactVelocity);
                            outAffectedPlayers[i].GetComponent<Rigidbody>().AddForce(ComputePushModifier(outAffectedPlayers[i].gameObject) * slamImpactVelocity);
                            outAffectedPlayers[i].GetComponent<CharacterInfo>().ragePercent += ComputeDamageToApply(slamImpactVelocity.magnitude);

                        }
                    }
                    state = MovementState.Jumping;
                }
                ClearRequests();
                break;

            default:
                break;
        }

        if (GetComponent<Rigidbody>().velocity.magnitude < maxSpeed) { GetComponent<Rigidbody>().AddForce(velocity, ForceMode.VelocityChange); }
    }

    public void RequestMoveForward(float axisValue) { verticleAxisValue = axisValue; }
    public void RequestMoveRight(float axisValue) { horizontalAxisValue = axisValue; }
    public void RequestJump() { shouldJump = true; }
    public void RequestDash() { shouldDash = true; }
    public void RequestSlam() { shouldSlam = true; }
    private void ClearRequests()
    {
        shouldJump = false;
        shouldDash = false;
        shouldSlam = false;
    }

    private void MoveForward(float axisValue) { velocity.z = axisValue * speedForwardMin; }
    private void MoveRight(float axisValue) { velocity.x = axisValue * speedRightMin; }
    private void Jump() { GetComponent<Rigidbody>().AddForce(Vector3.up * speedJump, ForceMode.VelocityChange); }
    public void Dash() { GetComponent<Rigidbody>().AddForce(velocity.normalized * speedDash, ForceMode.VelocityChange); }
    public void Slam()
    {
        //// absolute slam
        //GetComponent<Rigidbody>().velocity = -Vector3.up * speedSlam;

        // relative slam
        GetComponent<Rigidbody>().AddForce(-Vector3.up * speedSlam, ForceMode.VelocityChange);
    }

    public bool IsMovingOnGround()
    {
        RaycastHit outHit;
        if (Physics.Raycast(transform.TransformPoint(GetComponent<CapsuleCollider>().center), Vector3.down,out outHit, 10f))
        {
            Vector3 point0 = transform.TransformPoint(GetComponent<CapsuleCollider>().center + Vector3.down * (GetComponent<CapsuleCollider>().height / 2 - GetComponent<CapsuleCollider>().radius));
            Vector3 point1 = transform.TransformPoint(GetComponent<CapsuleCollider>().center + Vector3.up * (GetComponent<CapsuleCollider>().height / 2 - GetComponent<CapsuleCollider>().radius));

            Collider[] cols = Physics.OverlapCapsule(point0, point1, GetComponent<CapsuleCollider>().radius * groundCheckPercent * transform.localScale.x, ~layerMask);
            foreach(Collider col in cols)
            {
                if(col.transform == outHit.transform)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private Vector3 ComputeVectorSelfBottomToObj(GameObject obj)
    {
        return obj.GetComponent<Renderer>().bounds.center - GetLowestPoint();
    }

    private Vector3 GetLowestPoint()
    {
        Vector3 center = GetComponent<Renderer>().bounds.center;
        float radius = GetComponent<Renderer>().bounds.extents.magnitude;
        return center - Vector3.up * radius;
    }

    private Collider[] GetDashedColliders()
    {
        Vector3 point0 = transform.TransformPoint(GetComponent<CapsuleCollider>().center + Vector3.down * (GetComponent<CapsuleCollider>().height / 2 - GetComponent<CapsuleCollider>().radius));
        Vector3 point1 = transform.TransformPoint(GetComponent<CapsuleCollider>().center + Vector3.up * (GetComponent<CapsuleCollider>().height / 2 - GetComponent<CapsuleCollider>().radius));

        Collider[] cols = Physics.OverlapCapsule(point0, point1, GetComponent<CapsuleCollider>().radius * groundCheckPercent * transform.localScale.x, layerMask);
        return cols;
    }


    public MovementState GetMovementState() { return state; }

    public float ComputePushModifier(GameObject obj)
    {
        return (1 + obj.GetComponent<CharacterInfo>().ragePercent / 100) * (1 + GetComponent<CharacterInfo>().ragePercent / 100) / obj.GetComponent<Rigidbody>().mass;
    }

    public float ComputeDamageToApply(float damage)
    {
        return (1+GetComponent<CharacterInfo>().ragePercent/100) * damage * attackMod;
    }
}
