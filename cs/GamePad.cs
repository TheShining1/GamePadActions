using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;

public class CPHInline
{
  static string ActionsGroup = "GamePad_Actions";
  static string LogPrefix = "GamePad :: ";
  static bool Enabled = false;
  static Dictionary<string, string> joyActionsDict = new Dictionary<string, string>();
  static List<int> joyIds = new List<int>();
  
	enum ActionType
  {
    Action = 4,
    Comment = 1009
  }

  [Flags]
  enum Buttons : short
  {
    A = 1,
    B = 2,
    X = 4,
    Y = 8,
    LB = 16,
    RB = 32,
    View = 64,
    Share = 128,
    LS = 256,
    RS = 512
  }

  [DllImport("Winmm.dll")]
  static extern int joyGetPosEx(int uJoyID, ref JOYINFOEX pji);
  [DllImport("Winmm.dll")]
  static extern int joyGetNumDevs();
  [StructLayout(LayoutKind.Sequential)]

  struct JOYINFOEX
  {
    public int dwSize;
    public int dwFlags;
    public int dwXpos;
    public int dwYpos;
    public int dwZpos;
    public int dwRpos;
    public int dwUpos;
    public int dwVpos;
    public int dwButtons;
    public int dwButtonNumber;
    public int dwPOV;
    public int dwReserved1;
    public int dwReserved2;
  }

  enum MMSYSERR
  {
    BASE = 0,
    BADDEVICEID = (BASE + 5),
    INVALPARAM = (BASE + 11)
	}

  enum JOYERR
  {
    BASE = 160,
    PARMS = (BASE + 5),
    UNPLUGGED = (BASE + 7)
	}

  /*
	public void Init()
	{
		Start();
	}
*/
  public bool Execute()
  {
    var JoyArr = new JOYINFOEX();
    JoyArr.dwSize = Marshal.SizeOf(JoyArr);
    JoyArr.dwFlags = 255;
    var JoyId = joyIds[0];
    while (Enabled)
    {
      var err = joyGetPosEx(JoyId, ref JoyArr);
      if (err != (int)MMSYSERR.BASE)
      {
        var errString = Enum.GetName(typeof(MMSYSERR), err) + Enum.GetName(typeof(JOYERR), err);
        CPH.LogDebug($"{LogPrefix}ID: {JoyId}, error {err}:{errString}");
        Stop();
        return false;
      }

      foreach (KeyValuePair<string, string> action in joyActionsDict)
      {
        int sequence = 0;
        string[] keysArr = action.Key.Split('+');
        foreach (string key in keysArr)
        {
          Buttons button = (Buttons)Enum.Parse(typeof(Buttons), key);
          sequence = sequence | (int)button;
        }

        var keyPressed = isPressed(JoyArr, (int)sequence, keysArr.Length);
        if (keyPressed)
        {
          CPH.LogDebug($"{LogPrefix}ID {JoyId}, Sequence: {sequence} is pressed");
          var isRun = CPH.RunActionById(action.Value);
          CPH.LogDebug($"{LogPrefix}Action {action.Value} is run: {isRun}");
        }
      }

      Thread.Sleep(100);
    }

    return true;
  }

  bool isPressed(JOYINFOEX JoyArr, int sequence, int numberOfButtons)
  {
    return (JoyArr.dwButtons & sequence) == sequence && JoyArr.dwButtonNumber == numberOfButtons;
  }

  public bool Start()
  {
    CPH.LogDebug($"{LogPrefix}started listening");
    joyIds = FindJoysticks();
    if (joyIds.Count == 0)
    {
      Stop();
      return false;
    }

    joyActionsDict = GetActions(ActionsGroup);
    Enabled = true;
    Execute();
    return true;
  }

  public bool Stop()
  {
    CPH.LogDebug($"{LogPrefix}stopped listening");
    Enabled = false;
    return true;
  }

  List<int> FindJoysticks()
  {
    CPH.LogDebug($"{LogPrefix}looking for gamepads");
    var ids = new List<int>();
    var JoyArr = new JOYINFOEX();
    JoyArr.dwSize = Marshal.SizeOf(JoyArr);
    JoyArr.dwFlags = 255;
    var numOfJoys = joyGetNumDevs();
    for (var i = 0; i < numOfJoys; i++)
    {
      var err = joyGetPosEx(i, ref JoyArr);
      CPH.LogDebug($"{LogPrefix}err {err}");
      if (err != 0)
        continue;
      ids.Add(i);
    }

    CPH.LogDebug($"{LogPrefix}found {ids.Count} gamepads with IDs {String.Join(", ", ids)} ");
    return ids;
  }

  Dictionary<string, string> GetActions(string groupName)
  {
    string filePath = @"data\actions.json";
    string jsonStr = File.ReadAllText(filePath);

    var joyActionsDict = new Dictionary<string, string>();

    using (JsonDocument jsonDoc = JsonDocument.Parse(jsonStr))
    {
      JsonElement root = jsonDoc.RootElement;
      JsonElement actionsElements = root.GetProperty("actions");
      
      foreach (JsonElement action in actionsElements.EnumerateArray())
      {
        var actionsDict = new Dictionary<string, string>();
        string actionGroup = action.GetProperty("group").GetString();
        bool actionEnabled = action.GetProperty("enabled").GetBoolean();
        if (actionEnabled && actionGroup == groupName)
        {
          JsonElement subActionsElements = action.GetProperty("subActions");
          string buttons = "";
          string actionId = "";
          foreach (JsonElement subActionElement in subActionsElements.EnumerateArray())
          {
            if (subActionElement.GetProperty("type").GetInt32() == (int)ActionType.Comment)
              buttons = subActionElement.GetProperty("value").GetString();
            if (subActionElement.GetProperty("type").GetInt32() == (int)ActionType.Action)
              actionId = subActionElement.GetProperty("actionId").GetString();
          }

          if (buttons == "" || actionId == "")
            continue;
						
          joyActionsDict.Add(buttons, actionId);
        }
      }
    }

    foreach (KeyValuePair<string, string> action in joyActionsDict)
    {
      CPH.LogInfo($"{LogPrefix}Found buttons: {action.Key} actions: {action.Value}");
    }

    return joyActionsDict;
  }
}