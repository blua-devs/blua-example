using UnityEngine;
using bLua;
using bLua.ExampleUserData;
#if UNITY_EDITOR
using UnityEditor;
#endif // UNITY_EDITOR

#if UNITY_EDITOR
[CustomEditor(typeof(bLuaComponent))]
public class bLuaComponentEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (EditorApplication.isPlaying)
        {
            GUILayout.Space(20);

            bLuaComponent component = target as bLuaComponent;
            if (component != null)
            {
                if (!component.ranCode)
                {
                    if (GUILayout.Button("Run Code"))
                    {
                        component.RunCode();
                    }                    
                }
            }
        }
    }
}
#endif // UNITY_EDITOR

public static class bLuaGlobal
{
    public static bLuaInstance instance;

    
    static bLuaGlobal()
    {
        bLuaSettings settings = new bLuaSettings()
        {
            features = bLuaSettings.SANDBOX_ALL,
            tickBehavior = bLuaSettings.TickBehavior.TickAtInterval,
            coroutineBehaviour = bLuaSettings.CoroutineBehaviour.ResumeOnTick
        };
        
        instance = new bLuaInstance(settings);
        
        instance.OnPrint.AddListener(OnLuaPrint);
    }

    
    private static void OnLuaPrint(bLuaValue[] args)
    {
        string log = "";
        foreach (bLuaValue arg in args)
        {
            log += arg.ToString();
        }
        Debug.Log(log);
    }
}

public class bLuaComponent : MonoBehaviour
{
    private bLuaValue environment;

    /// <summary>
    /// When true, run code when Unity's Start event fires. If this is set to false, you will need to manually call RunCode() to run the code.
    /// </summary>
    [SerializeField] private bool runCodeOnStart = true;

    /// <summary>
    /// When true, attempt to run Lua functions with the following names: "Start, Update, OnDestroy" when their respective Unity functions
    /// are called by Unity. You do not need to add any or all of these functions if you don't want to.
    /// </summary>
    [SerializeField] private bool runMonoBehaviourEvents = true;

    /// <summary>
    /// The name of the Lua chunk. Used for debug information and error messages.
    /// </summary>
    [SerializeField] private string chunkName = "default_component";

    /// <summary>
    /// The code that will be run on this component.
    /// </summary>
    [SerializeField]
    [TextArea(2, 512)]
    private string code;
    
    public bool ranCode { get; private set; }
    

    private void Awake()
    {
        // Set up the global environment with any properties and functions we want
        environment = bLuaValue.CreateTable(bLuaGlobal.instance);
        environment.Set("Vector3", new bLuaVector3Library());
        environment.Set("GameObject", new bLuaGameObjectLibrary());
        environment.Set("gameObject", new bLuaGameObject(gameObject));
    }

    private void Start()
    {
        if (runCodeOnStart)
        {
            RunCode();
        }

        if (runMonoBehaviourEvents)
        {
            RunEvent("Start");
        }
    }

    private void Update()
    {
        if (runMonoBehaviourEvents)
        {
            RunEvent("Update");
        }
    }

    private void OnDestroy()
    {
        if (runMonoBehaviourEvents)
        {
            RunEvent("OnDestroy");
        }
    }


    public void RunCode()
    {
        if (!ranCode)
        {
            bLuaGlobal.instance.DoString(code, chunkName, environment);
            ranCode = true;
        }
    }

    public void RunEvent(string _name)
    {
        if (ranCode)
        {
            bLuaValue func = environment.Get(_name);
            if (func != null
                && func.luaType == LuaType.Function)
            {
                bLuaGlobal.instance.CallAsCoroutine(func);
            }
        }
    }
}
