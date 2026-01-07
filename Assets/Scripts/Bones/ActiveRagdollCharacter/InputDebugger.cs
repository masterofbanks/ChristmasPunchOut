using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Standalone input debugger to verify input system is working
/// Attach to character to see raw input events
/// </summary>
public class InputDebugger : MonoBehaviour
{
    private PlayerInput playerInput;

    private void Awake()
    {
        Debug.Log("=== [InputDebugger] AWAKE ===");
        playerInput = GetComponent<PlayerInput>();

        if (playerInput == null)
        {
            Debug.LogError("[InputDebugger] ❌ NO PlayerInput component!");
            return;
        }

        Debug.Log($"[InputDebugger] ✓ PlayerInput found");

        // Subscribe to ALL actions manually to see what fires
        if (playerInput.actions != null)
        {
            foreach (var actionMap in playerInput.actions.actionMaps)
            {
                Debug.Log($"[InputDebugger] Action Map: {actionMap.name}");

                foreach (var action in actionMap.actions)
                {
                    Debug.Log($"  - Action: {action.name}");

                    // Subscribe to every action
                    action.performed += (ctx) =>
                    {
                        Debug.Log($"🔥 ACTION PERFORMED: {action.name} = {ctx.ReadValueAsObject()}");
                    };

                    action.started += (ctx) =>
                    {
                        Debug.Log($"▶ ACTION STARTED: {action.name}");
                    };

                    action.canceled += (ctx) =>
                    {
                        Debug.Log($"⏹ ACTION CANCELED: {action.name}");
                    };
                }
            }
        }
    }

    private void Update()
    {
        // Also check legacy input as a sanity check
        if (Input.anyKeyDown)
        {
            Debug.Log($"[InputDebugger] Legacy Input detected: anyKey={Input.anyKey}");

            if (Input.GetKey(KeyCode.W)) Debug.Log("  W pressed");
            if (Input.GetKey(KeyCode.A)) Debug.Log("  A pressed");
            if (Input.GetKey(KeyCode.S)) Debug.Log("  S pressed");
            if (Input.GetKey(KeyCode.D)) Debug.Log("  D pressed");
        }
    }
}