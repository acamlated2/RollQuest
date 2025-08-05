using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static InputManager instance;
    
    [SerializeField] private PlayerInput playerInput;

    private Dictionary<string, List<Action<InputAction.CallbackContext>>> boundActions =
        new Dictionary<string, List<Action<InputAction.CallbackContext>>>();
    
    public InputAction xMovement;
    public InputAction yMovement;
    public InputAction zMovement;
    public InputAction speedControl;
    
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
    }

    private void Start()
    {
        
        BindAction("MoveFB", PlayerScript.instance.MoveFB);
        BindAction("MoveLR", PlayerScript.instance.MoveLR);
        BindAction("Look", CameraScript.instance.RotateCamera);
        
        // if (GameHelperScript.instance != null)
        // {
        //     BindAction("Access Debug Keys", GameHelperScript.instance.AccessDebugKeys);
        //     BindAction("Screenshot", GameHelperScript.instance.ScreenShot);
        //     BindAction("Clear Save", GameHelperScript.instance.ClearSave);
        //     BindAction("Add Waves", GameHelperScript.instance.AddWaves);
        //     BindAction("Add Coins", GameHelperScript.instance.AddCoins);
        //     BindAction("Add Resources", GameHelperScript.instance.AddResources);
        //     BindAction("Add Debug Divider", GameHelperScript.instance.AddDebugDivider);
        // }

        // if (GameStateControllerScript.Instance != null && GameControllerScript.instance != null)
        // {
        //     BindAction("Back", new List<Action<InputAction.CallbackContext>>()
        //     {
        //         GameStateControllerScript.Instance.Back,
        //         GameControllerScript.instance.Back
        //     });
        // }

        //BindAction("Show Keybinds", ShowKeybinds);
    }

    private void BindAction(string actionName, Action<InputAction.CallbackContext> context)
    {
        InputAction action = playerInput.actions.FindAction(actionName);
        action.Enable();
        action.performed += context;
        action.canceled += context;
        
        if (!boundActions.ContainsKey(actionName))
        {
            boundActions[actionName] = new List<Action<InputAction.CallbackContext>>();
        }

        boundActions[actionName].Add(context);
    }
    
    private void BindAction(string actionName, List<Action<InputAction.CallbackContext>> contexts)
    {
        InputAction action = playerInput.actions[actionName];
        action.Enable();

        foreach (Action<InputAction.CallbackContext> context in contexts)
        {
            action.performed += context;
            action.canceled += context;   
            
            if (!boundActions.ContainsKey(actionName))
            {
                boundActions[actionName] = new List<Action<InputAction.CallbackContext>>();
            }

            boundActions[actionName].Add(context);
        }
    }
    
    private void BindAction(string actionName, Action<InputAction.CallbackContext> context, ref InputAction inputAction)
    {
        InputAction action = playerInput.actions.FindAction(actionName);
        action.Enable();
        action.performed += context;
        action.canceled += context;
        
        if (!boundActions.ContainsKey(actionName))
        {
            boundActions[actionName] = new List<Action<InputAction.CallbackContext>>();
        }

        boundActions[actionName].Add(context);

        inputAction = action;
    }
    
    private void OnDestroy()
    {
        if (playerInput == null)
        {
            return;
        }
        
        foreach (var kvp in boundActions)
        {
            var action = playerInput.actions[kvp.Key];
            foreach (Action<InputAction.CallbackContext> callback in kvp.Value)
            {
                action.performed -= callback;
                action.canceled -= callback;
            }
        }
    }

    private void Update()
    {
        // if (GameControllerScript.instance.currentScene == GameControllerScript.SceneType.Game)
        // {
        //     CheckInputAndFinishTask(xMovement,
        //                             TutorialDisplayScript.TutorialTask.PressD,
        //                             TutorialDisplayScript.TutorialTask.PressA);
        //     CheckInputAndFinishTask(yMovement,
        //                             TutorialDisplayScript.TutorialTask.PressE,
        //                             TutorialDisplayScript.TutorialTask.PressQ);
        //     CheckInputAndFinishTask(zMovement,
        //                             TutorialDisplayScript.TutorialTask.PressW,
        //                             TutorialDisplayScript.TutorialTask.PressS);
        //     CheckInputAndFinishTask(speedControl,
        //                             TutorialDisplayScript.TutorialTask.PressShift,
        //                             TutorialDisplayScript.TutorialTask.PressCtrl);
        // }
    }

    // private void CheckInputAndFinishTask(InputAction action,
    //                                      TutorialDisplayScript.TutorialTask positiveTask,
    //                                      TutorialDisplayScript.TutorialTask negativeTask)
    // {
    //     float value = action.ReadValue<float>();
    //     
    //     if (value > 0.1f)
    //     {
    //         TutorialDisplayScript.instance.FinishTask(positiveTask);
    //     }
    //     else if (value < -0.1f)
    //     {
    //         TutorialDisplayScript.instance.FinishTask(negativeTask);
    //     }
    // }

    // private void ShowKeybinds(InputAction.CallbackContext context)
    // {
    //     if (context.ReadValue<float>() <= 0)
    //     {
    //         return;
    //     }
    //     
    //     if (GameDebugLogScript.instance != null)
    //     {
    //         GameDebugLogScript.instance.AddText("Showing Keybinds: ", Color.white);
    //     }
    //     else
    //     {
    //         Debug.Log("Showing Keybinds: ");
    //     }
    //     
    //     foreach (InputAction action in playerInput.actions)
    //     {
    //         string keybind = action.name + ": ";
    //         
    //         foreach (var binding in action.bindings)
    //         {
    //             string bindingPath = binding.path;
    //             keybind += bindingPath + ", ";
    //         }
    //         
    //         if (GameDebugLogScript.instance != null)
    //         {
    //             GameDebugLogScript.instance.AddText(keybind, Color.white);
    //         }
    //         else
    //         {
    //             Debug.Log(keybind);
    //         }
    //     }
    // }
}