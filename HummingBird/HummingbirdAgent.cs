using System;
using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A hummingbird Machine Learning Agent
/// </summary>
public class HummingbirdAgent : Agent
{
    [Tooltip("Force to apply when moving")] public float moveForce = 2f;
    [Tooltip("Speed to pitch up or down")] public float pitchSpeed = 100f;
    [Tooltip("Speed to rotate around the up axis")] public float yawSpeed = 100f;
    [Tooltip("Transform at the tip of the beak")] public Transform beakTip;
    [Tooltip("The agent's camera")] public Camera agentCamera;
    [Tooltip("Whether this is training mode or gameplay mode")] public bool trainingMode;

    // Components & world refs
    new private Rigidbody rigidbody;
    private FlowerArea flowerArea;

    // State
    private Flower nearestFlower;
    private float smoothPitchChange = 0f;
    private float smoothYawChange   = 0f;
    private const float MaxPitchAngle = 80f;
    private const float BeakTipRadius = 0.008f;
    private bool frozen = false;

    /// <summary>Amount of nectar obtained this episode</summary>
    public float NectarObtained { get; private set; }

    public override void Initialize()
    {
        rigidbody  = GetComponent<Rigidbody>();
        flowerArea = GetComponentInParent<FlowerArea>();
        if (!trainingMode) MaxStep = 0; // play forever in non-training
    }

    public override void OnEpisodeBegin()
    {
        if (trainingMode) flowerArea.ResetFlowers();

        NectarObtained = 0f;
        rigidbody.velocity        = Vector3.zero;
        rigidbody.angularVelocity = Vector3.zero;

        bool inFrontOfFlower = !trainingMode || UnityEngine.Random.value > .5f;
        MoveToSafeRandomPosition(inFrontOfFlower);
        UpdateNearestFlower();
    }

    /// <summary>vectorAction: 0:x, 1:y, 2:z, 3:pitch, 4:yaw</summary>
    public override void OnActionReceived(ActionBuffers vectorAction)
    {
        if (frozen) return;

        var va = vectorAction.ContinuousActions;

        // Move
        Vector3 move = new Vector3(va[0], va[1], va[2]);
        rigidbody.AddForce(move * moveForce);

        // Rotate
        Vector3 rot = transform.rotation.eulerAngles;
        float pitchChange = va[3];
        float yawChange   = va[4];

        smoothPitchChange = Mathf.MoveTowards(smoothPitchChange, pitchChange, 2f * Time.fixedDeltaTime);
        smoothYawChange   = Mathf.MoveTowards(smoothYawChange,   yawChange,   2f * Time.fixedDeltaTime);

        float pitch = rot.x + smoothPitchChange * Time.fixedDeltaTime * pitchSpeed;
        if (pitch > 180f) pitch -= 360f;
        pitch = Mathf.Clamp(pitch, -MaxPitchAngle, MaxPitchAngle);

        float yaw = rot.y + smoothYawChange * Time.fixedDeltaTime * yawSpeed;

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (nearestFlower == null)
        {
            sensor.AddObservation(new float[10]);
            return;
        }

        // 4: local rotation
        sensor.AddObservation(transform.localRotation.normalized);

        // vector to flower
        Vector3 toFlower = nearestFlower.FlowerCenterPosition - beakTip.position;
        sensor.AddObservation(toFlower.normalized); // 3

        // beak-in-front & facing
        sensor.AddObservation(Vector3.Dot(toFlower.normalized, -nearestFlower.FlowerUpVector.normalized));            // 1
        sensor.AddObservation(Vector3.Dot(beakTip.forward.normalized, -nearestFlower.FlowerUpVector.normalized));     // 1

        // normalized distance
        sensor.AddObservation(toFlower.magnitude / FlowerArea.AreaDiameter);                                          // 1
        // = 10 total
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var a = actionsOut.ContinuousActions;

        Vector3 fwd = Vector3.zero, strafe = Vector3.zero, up = Vector3.zero;
        float pitch = 0f, yaw = 0f;

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed) fwd = transform.forward;
            else if (kb.sKey.isPressed) fwd = -transform.forward;

            if (kb.aKey.isPressed) strafe = -transform.right;
            else if (kb.dKey.isPressed) strafe = transform.right;

            if (kb.spaceKey.isPressed) up = Vector3.up;
            else if (kb.leftCtrlKey.isPressed) up = Vector3.down;

            if (kb.upArrowKey.isPressed)      pitch = 1f;
            else if (kb.downArrowKey.isPressed) pitch = -1f;

            if (kb.rightArrowKey.isPressed)   yaw = 1f;
            else if (kb.leftArrowKey.isPressed)  yaw = -1f;
        }

        Vector3 move = Vector3.ClampMagnitude(fwd + strafe + up, 1f);
        a[0] = Mathf.Clamp(move.x, -1f, 1f);
        a[1] = Mathf.Clamp(move.y, -1f, 1f);
        a[2] = Mathf.Clamp(move.z, -1f, 1f);
        a[3] = Mathf.Clamp(pitch, -1f, 1f);
        a[4] = Mathf.Clamp(yaw,   -1f, 1f);
    }

    public void FreezeAgent()
    {
        Debug.Assert(trainingMode == false, "Freeze/Unfreeze not supported in training");
        frozen = true; rigidbody.Sleep();
    }

    public void UnfreezeAgent()
    {
        Debug.Assert(trainingMode == false, "Freeze/Unfreeze not supported in training");
        frozen = false; rigidbody.WakeUp();
    }

    private void MoveToSafeRandomPosition(bool inFrontOfFlower)
    {
        bool safe = false;
        int attempts = 100;
        Vector3 pos = Vector3.zero;
        Quaternion rot = Quaternion.identity;

        while (!safe && attempts-- > 0)
        {
            if (inFrontOfFlower)
            {
                Flower randomFlower = flowerArea.Flowers[UnityEngine.Random.Range(0, flowerArea.Flowers.Count)];
                float dist = UnityEngine.Random.Range(.1f, .2f);
                pos = randomFlower.transform.position + randomFlower.FlowerUpVector * dist;

                Vector3 toFlower = randomFlower.FlowerCenterPosition - pos;
                rot = Quaternion.LookRotation(toFlower, Vector3.up);
            }
            else
            {
                float height = UnityEngine.Random.Range(1.2f, 2.5f);
                float radius = UnityEngine.Random.Range(2f, 7f);
                Quaternion dir = Quaternion.Euler(0f, UnityEngine.Random.Range(-180f, 180f), 0f);
                pos = flowerArea.transform.position + Vector3.up * height + dir * Vector3.forward * radius;

                float p = UnityEngine.Random.Range(-60f, 60f);
                float y = UnityEngine.Random.Range(-180f, 180f);
                rot = Quaternion.Euler(p, y, 0f);
            }

            safe = Physics.OverlapSphere(pos, 0.05f).Length == 0;
        }

        Debug.Assert(safe, "Could not find a safe position to spawn");
        transform.position = pos;
        transform.rotation = rot;
    }

    private void UpdateNearestFlower()
    {
        foreach (Flower flower in flowerArea.Flowers)
        {
            if (nearestFlower == null && flower.HasNectar)
            {
                nearestFlower = flower;
            }
            else if (flower.HasNectar)
            {
                float dNew = Vector3.Distance(flower.transform.position, beakTip.position);
                float dOld = Vector3.Distance(nearestFlower.transform.position, beakTip.position);
                if (!nearestFlower.HasNectar || dNew < dOld) nearestFlower = flower;
            }
        }
    }

    private void OnTriggerEnter(Collider other) => TriggerEnterOrStay(other);
    private void OnTriggerStay(Collider other)  => TriggerEnterOrStay(other);

    /// <summary>Uses the FlowerArea lookup (no tags) to detect nectar hits.</summary>
    private void TriggerEnterOrStay(Collider other)
    {
        Flower flower = flowerArea.GetFlowerFromNectar(other);
        if (flower == null) return;

        Vector3 closest = other.ClosestPoint(beakTip.position);
        if (Vector3.Distance(beakTip.position, closest) >= BeakTipRadius) return;

        float received = flower.Feed(0.01f);
        NectarObtained += received;

        if (trainingMode)
        {
            float facing = Mathf.Clamp01(Vector3.Dot(transform.forward.normalized, -flower.FlowerUpVector.normalized));
            AddReward(0.10f + 0.02f * facing);
        }

        if (!flower.HasNectar) UpdateNearestFlower();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (trainingMode && collision.collider.CompareTag("boundary"))
            AddReward(-1f);
    }

    private void Update()
    {
        if (nearestFlower != null)
            Debug.DrawLine(beakTip.position, nearestFlower.FlowerCenterPosition, Color.green);
    }

    private void FixedUpdate()
    {
        if (nearestFlower != null && !nearestFlower.HasNectar)
            UpdateNearestFlower();
    }
}
