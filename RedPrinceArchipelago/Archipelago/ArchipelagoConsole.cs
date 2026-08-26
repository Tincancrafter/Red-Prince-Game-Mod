using BepInEx;
using BepInEx.Unity.IL2CPP.UnityEngine;
using RedPrinceArchipelago.Items;
using RedPrinceArchipelago.Models;
using RedPrinceArchipelago.Rooms;
using RedPrinceArchipelago.Utils;
using StableNameDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace RedPrinceArchipelago.Archipelago;

/// <summary>
///     Shamelessly stolen from oc2-modding https://github.com/toasterparty/oc2-modding/blob/main/OC2Modding/GameLog.cs with modifications for Blue Prince.
/// </summary>
public static class ArchipelagoConsole
{
    public static bool Hidden = true;
    public static bool ShowOnlyRelevantMessages = true;

    private static List<string> logLines = new();
    private static Vector2 scrollView;
    private static Rect window;
    private static Rect scroll;
    private static Rect text;
    private static Rect hideShowButton;
    private static Rect connectionPanel;

    private static GUIStyle textStyle = new();
    private static GUIStyle headingStyle;
    private static GUIStyle statusStyle;
    private static GUIStyle fieldLabelStyle;
    private static string scrollText = "";
    private static string previousScrollText = "";
    private static int previousStart = 0;
    private static int previousEnd = 0;
    private static float lastUpdateTime = Time.time;
    private const float HideTimeout = 15f;
    private const int MaxLogLines = 300;

    private static string CommandText = "/help";
    private static Rect CommandTextRect;
    private static Rect SendCommandButton;
    private static List<string> PreviousCommands = [];
    private static int PreviousCommandPointer = -1;
    private static List<string> TextFieldNames = ["URI", "SlotName", "Password", "CommandText"];
    private static bool ShowPassword;
    private static string ConnectionStatus = "Enter the server details for this slot.";
    private static bool ConnectionStatusIsError;
    private static readonly HttpClient UpdateClient = new();
    private static string UpdateButtonText = "Checking...";
    private static string LatestReleaseUrl = "https://github.com/Tincancrafter/Red-Prince-Releases/releases/latest";
    private static bool UpdateCheckComplete;
    private static bool UpdateAvailable;
    private static bool UpdateCheckRunning;

    /// <summary>
    ///     Unity Monobehaviour Awake()
    /// </summary>
    public static void Awake()
    {
        UpdateWindow();
        CheckForUpdates();
    }

    private static async void CheckForUpdates()
    {
        if (UpdateCheckRunning) return;

        UpdateCheckRunning = true;
        UpdateCheckComplete = false;
        UpdateButtonText = "Checking...";
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get,
                "https://api.github.com/repos/Tincancrafter/Red-Prince-Releases/releases/latest");
            request.Headers.UserAgent.ParseAdd($"RedPrinceArchipelago/{Plugin.PluginVersion}");
            using HttpResponseMessage response = await UpdateClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            JObject release = JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            string latestTag = release.Value<string>("tag_name")?.TrimStart('v', 'V') ?? Plugin.PluginVersion;
            LatestReleaseUrl = release.Value<string>("html_url") ?? LatestReleaseUrl;

            UpdateAvailable = Version.TryParse(latestTag, out Version latestVersion) &&
                Version.TryParse(Plugin.PluginVersion, out Version installedVersion) &&
                latestVersion > installedVersion;
            UpdateButtonText = UpdateAvailable ? $"Update {latestTag}" : "Latest";
            UpdateCheckComplete = true;
        }
        catch (Exception exception)
        {
            UpdateAvailable = false;
            UpdateButtonText = "Retry update";
            Logging.LogWarning($"Unable to check for plugin updates: {exception.Message}", "UpdateCheck");
        }
        finally
        {
            UpdateCheckRunning = false;
        }
    }

    public static void ShowConnectionPrompt()
    {
        Hidden = false;
        UpdateWindow();
    }

    public static void SetConnectionStatus(string message, bool isError)
    {
        ConnectionStatus = message;
        ConnectionStatusIsError = isError;
        UpdateWindow();
    }

    /// <summary>
    ///     Logs a Message in the in Game Console.
    /// </summary>
    /// <param name="message">The Message to log.</param>
    /// <param name="logTag">The Tag of the message. Defaults to "ArchipelagoConsole"</param>
    /// <param name="isServerMessage">Whether the message is from the server.</param>
    public static void LogMessage(string message, string logTag = "ArchipelagoConsole", bool isServerMessage = false)
    {
        if (message.IsNullOrWhiteSpace()) return;

        //Handle multiline messages.
        // Log any relevant messages to the archipelago console;
        if (IsRelevantMessage(message))
        {
            Logging.Log(message, logTag);
        }
        if (message.Contains('\n'))
        {
            foreach (string submessage in message.Split("\n"))
            {
                logLines.Add(submessage);
            }
            lastUpdateTime = Time.time;
            UpdateWindow();
        }
        else
        {
            logLines.Add(message);
            lastUpdateTime = Time.time;
            UpdateWindow();
        }
    }

    /// <summary>
    ///     If the message should be logged to the bepinex console.
    /// </summary>
    /// <param name="message">The string message.</param>
    /// <returns>Returns true if the message should be logged. False Otherwise.</returns>
    private static bool IsRelevantMessage(string message)
    {
        if (!ShowOnlyRelevantMessages) return true;
        if (message.Contains(ArchipelagoClient.ServerData.SlotName) || message.Contains("[Server]") || message.Contains("You can't afford the hint")) return true;
        return false;
    }

    /// <summary>
    ///     To be run on a Unity OnGUI update.
    /// </summary>
    public static void OnGUI()
    {
        Event e = Event.current;
        //Shows the Input Window
        if (Hidden && Input.GetKeyInt(BepInEx.Unity.IL2CPP.UnityEngine.KeyCode.Slash))
        {
            Hidden = !Hidden;
            UpdateWindow();
        }
        if (!Hidden && Input.GetKeyInt(BepInEx.Unity.IL2CPP.UnityEngine.KeyCode.Escape))
        {
            Hidden = !Hidden;
            UpdateWindow();
        }
        if (!Hidden && e.type == EventType.KeyDown)
        {
            if (e.keyCode == UnityEngine.KeyCode.UpArrow)
            {
                if (PreviousCommandPointer > 0)
                {
                    CommandText = PreviousCommands[PreviousCommandPointer];
                    PreviousCommandPointer--;
                }
                else
                {
                    PreviousCommandPointer = PreviousCommands.Count - 1;

                }
            }
        }

        if (!Hidden || Time.time - lastUpdateTime < HideTimeout)
        {
            scrollView = GUI.BeginScrollView(window, scrollView, scroll);
            GUI.Box(text, "");
            GUI.Box(text, scrollText, textStyle);
            GUI.EndScrollView();
        }

        if (GUI.Button(hideShowButton, Hidden ? "Show" : "Hide"))
        {
            Hidden = !Hidden;
            //PreviousCursorLockstate = Cursor.lockState;
            UpdateWindow();
        }

        // draw client/server commands entry if not hidden.
        if (Hidden) {
            //When the console is hidden make sure keyboard controls are selectable.
            ToggleKeyboardInput(false);
            return;
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        // Prevents tabbing from affecting the GUI fields (Was getting really annoying with alt-tabbing)
        if (Event.current.type == EventType.KeyDown && (Event.current.keyCode == UnityEngine.KeyCode.Tab || Event.current.character == '\t'))
        {
            Event.current.Use(); // Marks the event as used, stopping propagation
        }

        DrawConnectionPanel(e);
        GUI.SetNextControlName("CommandText");
        CommandText = GUI.TextField(CommandTextRect, CommandText);
        if (!CommandText.IsNullOrWhiteSpace() && (GUI.Button(SendCommandButton, "Send") || e.type == EventType.KeyDown && (e.keyCode == UnityEngine.KeyCode.Return || e.character == '\n')))
        {
            //local command
            if (CommandText.Trim()[0] == '/')
            {
                CommandManager.RunLocalCommand(CommandText);
                PreviousCommands.Add(CommandText);
                CommandText = "";
                PreviousCommandPointer = -1;
            }
            else if (ArchipelagoClient.Authenticated)
            {
                Plugin.ArchipelagoClient.SendMessage(CommandText);
                PreviousCommands.Add(CommandText);
                CommandText = "";
                PreviousCommandPointer = -1;
            }
        }
        ToggleKeyboardInput(TextFieldNames.Contains(GUI.GetNameOfFocusedControl()));
    }

    private static void DrawConnectionPanel(Event currentEvent)
    {
        EnsureConnectionStyles();
        GUI.Box(connectionPanel, GUIContent.none);

        float padding = Math.Max(12f, connectionPanel.width * 0.025f);
        float rowHeight = Math.Max(22f, Screen.height * 0.026f);
        float x = connectionPanel.x + padding;
        float y = connectionPanel.y + padding;
        float contentWidth = connectionPanel.width - padding * 2f;

        const float actionButtonWidth = 120f;
        const float actionButtonGap = 8f;
        float actionButtonsWidth = actionButtonWidth * 2f + actionButtonGap;
        GUI.Label(new Rect(x, y, contentWidth * 0.65f, rowHeight), "ARCHIPELAGO CONNECTION", headingStyle);
        statusStyle.normal.textColor = ArchipelagoClient.Authenticated
            ? new Color(0.45f, 0.9f, 0.55f)
            : ConnectionStatusIsError ? new Color(1f, 0.48f, 0.42f) : new Color(0.82f, 0.84f, 0.88f);
        string state = ArchipelagoClient.Authenticated ? "CONNECTED" : Plugin.ArchipelagoClient.IsAttemptingConnection ? "CONNECTING" : "DISCONNECTED";
        GUI.Label(new Rect(x + contentWidth * 0.65f, y, contentWidth * 0.35f, rowHeight), state, statusStyle);
        y += rowHeight + 6f;

        GUI.Label(new Rect(x, y, contentWidth - actionButtonsWidth - actionButtonGap, rowHeight),
            Plugin.ModDisplayInfo, fieldLabelStyle);
        bool previousUpdateEnabled = GUI.enabled;
        GUI.enabled = previousUpdateEnabled && (!UpdateCheckComplete || UpdateAvailable) && !UpdateCheckRunning;
        if (GUI.Button(new Rect(x + contentWidth - actionButtonsWidth, y, actionButtonWidth, rowHeight), UpdateButtonText))
        {
            if (UpdateCheckComplete && UpdateAvailable)
            {
                Application.OpenURL(LatestReleaseUrl);
            }
            else
            {
                CheckForUpdates();
            }
        }
        GUI.enabled = previousUpdateEnabled;
        bool previousHeaderEnabled = GUI.enabled;
        GUI.enabled = previousHeaderEnabled && (ArchipelagoClient.Authenticated || Plugin.ArchipelagoClient.IsAttemptingConnection);
        if (GUI.Button(new Rect(x + contentWidth - actionButtonWidth, y, actionButtonWidth, rowHeight), "Disconnect"))
        {
            Plugin.ArchipelagoClient.DisconnectFromServer();
        }
        GUI.enabled = previousHeaderEnabled;
        y += rowHeight + 8f;

        if (ArchipelagoClient.Authenticated)
        {
            GUI.Label(new Rect(x, y, contentWidth, rowHeight), $"{ArchipelagoClient.ServerData.SlotName}  |  {ArchipelagoClient.ServerData.Uri}");
            y += rowHeight;
            GUI.Label(new Rect(x, y, contentWidth, rowHeight), $"{Plugin.APDisplayInfo}  |  {Plugin.ModDisplayInfo}", fieldLabelStyle);
            return;
        }

        float labelWidth = Math.Max(105f, contentWidth * 0.2f);
        float revealWidth = Math.Max(92f, contentWidth * 0.18f);
        float fieldWidth = contentWidth - labelWidth;

        DrawConnectionField("Host", "URI", ref ArchipelagoClient.ServerData.Uri, x, y, labelWidth, fieldWidth, rowHeight, false);
        y += rowHeight + 5f;
        DrawConnectionField("Player name", "SlotName", ref ArchipelagoClient.ServerData.SlotName, x, y, labelWidth, fieldWidth, rowHeight, false);
        y += rowHeight + 5f;
        GUI.Label(new Rect(x, y, labelWidth, rowHeight), "Password", fieldLabelStyle);
        GUI.SetNextControlName("Password");
        Rect passwordRect = new Rect(x + labelWidth, y, fieldWidth - revealWidth - 6f, rowHeight);
        ArchipelagoClient.ServerData.Password = ShowPassword
            ? GUI.TextField(passwordRect, ArchipelagoClient.ServerData.Password)
            : GUI.PasswordField(passwordRect, ArchipelagoClient.ServerData.Password, '*');
        ShowPassword = GUI.Toggle(new Rect(passwordRect.xMax + 6f, y, revealWidth, rowHeight), ShowPassword, "Show");
        y += rowHeight + 8f;

        bool hasHost = !ArchipelagoClient.ServerData.Uri.IsNullOrWhiteSpace();
        bool hasSlot = !ArchipelagoClient.ServerData.SlotName.IsNullOrWhiteSpace();
        bool enterPressed = currentEvent.type == EventType.KeyDown &&
            (currentEvent.keyCode == UnityEngine.KeyCode.Return || currentEvent.keyCode == UnityEngine.KeyCode.KeypadEnter) &&
            TextFieldNames.Take(3).Contains(GUI.GetNameOfFocusedControl());

        bool previousEnabled = GUI.enabled;
        bool attemptingConnection = Plugin.ArchipelagoClient.IsAttemptingConnection;
        GUI.enabled = previousEnabled && (attemptingConnection || hasHost && hasSlot);
        bool connect = GUI.Button(new Rect(x + contentWidth - 120f, y, 120f, rowHeight),
            attemptingConnection ? "Cancel" : "Connect");
        GUI.enabled = previousEnabled;

        string helper = !hasHost ? "Enter a host and port." : !hasSlot ? "Enter the exact slot name." : ConnectionStatus;
        GUI.Label(new Rect(x, y, contentWidth - 130f, rowHeight), helper, fieldLabelStyle);
        if (connect && attemptingConnection)
        {
            currentEvent.Use();
            Plugin.ArchipelagoClient.DisconnectFromServer();
            return;
        }
        if ((connect || enterPressed) && hasHost && hasSlot && !Plugin.ArchipelagoClient.IsAttemptingConnection)
        {
            currentEvent.Use();
            ConnectionStatus = "Connecting...";
            ConnectionStatusIsError = false;
            State.UpdateServerDetails(new ConnectionData
            {
                Uri = ArchipelagoClient.ServerData.Uri.Trim(),
                SlotName = ArchipelagoClient.ServerData.SlotName.Trim(),
                Password = ArchipelagoClient.ServerData.Password
            });
            ArchipelagoClient.ServerData.Uri = ArchipelagoClient.ServerData.Uri.Trim();
            ArchipelagoClient.ServerData.SlotName = ArchipelagoClient.ServerData.SlotName.Trim();
            Plugin.ArchipelagoClient.Connect();
        }
    }

    private static void DrawConnectionField(string label, string controlName, ref string value, float x, float y,
        float labelWidth, float fieldWidth, float rowHeight, bool password)
    {
        GUI.Label(new Rect(x, y, labelWidth, rowHeight), label, fieldLabelStyle);
        GUI.SetNextControlName(controlName);
        value = password
            ? GUI.PasswordField(new Rect(x + labelWidth, y, fieldWidth, rowHeight), value, '*')
            : GUI.TextField(new Rect(x + labelWidth, y, fieldWidth, rowHeight), value);
    }

    private static void EnsureConnectionStyles()
    {
        if (headingStyle != null) return;
        headingStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
        statusStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold };
        fieldLabelStyle = new GUIStyle(GUI.skin.label);
        fieldLabelStyle.normal.textColor = new Color(0.72f, 0.75f, 0.8f);
    }

    /// <summary>
    ///     Whether to allow keyboard input to move the player charcter.
    /// </summary>
    /// <param name="focused">Whether the textfields are currently focused.</param>
    private static void ToggleKeyboardInput(bool focused) {
        var keyboard = Rewired.ReInput.controllers.Keyboard;
        
         if (focused)
         {
            if (keyboard.enabled)
            {
                keyboard.enabled = false;
            }
         }
         else
         {
            keyboard.enabled = true;
         }
    }

    /// <summary>
    ///     Performs the redraw and update of the console window UI.
    /// </summary>
    public static void UpdateWindow()
    {
        scrollText = "";
        int currentLogLines = logLines.Count;
        // Create a behind the scenes text form of the log that is cached;
        int start = Math.Max(0, currentLogLines - MaxLogLines);
        // If the scrolltext has not been initialized.
        if (previousScrollText == "")
        {
            for (var i = start; i < currentLogLines; i++)
            {
                previousScrollText += logLines[i];
                previousScrollText += "\n";
            }
            previousStart = start;
            previousEnd = currentLogLines;
        }
        // Otherwise use the previously cached string as a basis;
        else
        {
            string newLines = previousScrollText;
            // If the starting line has shifted, delete that many lines.
            if (start > previousStart)
            {
                int linesToDelete = start - previousStart;
                int index = 0;
                // Iterate through characters until the next newline is found, or the end is reached.
                while (linesToDelete > 0 && index < newLines.Length)
                {
                    char current = newLines[index];
                    if (current == '\n')
                    {
                        linesToDelete--;
                    }
                    index++;
                }
                // Update the new data;
                newLines = previousScrollText.Substring(index - 1);
                // Update the start to be the new start;
                previousStart = start;
            }
            // If a new line(s) got added, add them to the end of the cached scrolltext;
            // Cache the length in case extra lines get added while updating.
            int lengthDiff = currentLogLines - previousEnd;
            if (lengthDiff > 0)
            {
                for (int i = 0; i < lengthDiff; i++)
                {
                    newLines += logLines[previousEnd + i];
                    newLines += '\n';
                }
                previousEnd += lengthDiff;
            }
            // Finally set the scrollText to the new data;
            previousScrollText = newLines;
        }
        if (Hidden)
        {
            if (currentLogLines > 0)
            {
                scrollText = logLines[^1];
            }
        }
        else
        {
            scrollText = previousScrollText;
        }
       
        var width = (int)(Screen.width * 0.4f);
        int height;
        int scrollDepth;
        if (Hidden)
        {
            height = (int)(Screen.height * 0.03f);
            scrollDepth = height;
        }
        else
        {
            height = (int)(Screen.height * 0.3f);
            scrollDepth = height * 10;
        }

        window = new Rect(Screen.width / 2 - width / 2, 0, width, height);
        scroll = new Rect(0, 0, width * 0.9f, scrollDepth);
        scrollView = new Vector2(0, scrollDepth);
        text = new Rect(0, 0, width, scrollDepth);

        textStyle.alignment = TextAnchor.LowerLeft;
        textStyle.fontSize = (int)(Screen.height * 0.0165f);
        textStyle.normal.textColor = Color.white;
        textStyle.wordWrap = !Hidden;

        var xPadding = (int)(Screen.width * 0.01f);
        var yPadding = (int)(Screen.height * 0.01f);

        textStyle.padding = Hidden
            ? new RectOffset(xPadding / 2, xPadding / 2, yPadding / 2, yPadding / 2)
            : new RectOffset(xPadding, xPadding, yPadding, yPadding);

        var buttonWidth = (int)(Screen.width * 0.12f);
        var buttonHeight = (int)(Screen.height * 0.03f);

        hideShowButton = new Rect(Screen.width / 2 + width / 2 + buttonWidth / 3, Screen.height * 0.004f, buttonWidth,
            buttonHeight);

        float panelTop = Screen.height * 0.315f;
        float panelHeight = ArchipelagoClient.Authenticated
            ? Math.Max(130f, Screen.height * 0.14f)
            : Math.Max(235f, Screen.height * 0.26f);
        connectionPanel = new Rect(Screen.width / 2f - width / 2f, panelTop, width, panelHeight);

        // Draw server command text field and button below the connection panel.
        width = (int)(Screen.width * 0.4f);
        var xPos = (int)(Screen.width / 2.0f - width / 2.0f);
        var yPos = (int)(connectionPanel.yMax + Screen.height * 0.012f);
        height = Math.Max(22, (int)(Screen.height * 0.026f));

        CommandTextRect = new Rect(xPos, yPos, width, height);

        width = (int)(Screen.width * 0.035f);
        yPos += (int)(Screen.height * 0.03f);
        SendCommandButton = new Rect(xPos, yPos, width, height);
    }
}

/// <summary>
///     A Rudimentary manager for in game console commands.
/// </summary>
public static class CommandManager
{
    private static Dictionary<string, Command> _LocalCommands = new();
    private static Dictionary<string, Command> _ServerCommands = new();

    /// <summary>
    ///     Registers a local command.
    /// </summary>
    /// <param name="commandName">The name of the command.</param>
    /// <param name="command">The command Object to register.</param>
    public static void AddLocalCommand(string commandName, Command command)
    {
        _LocalCommands[commandName.Trim().ToLower()] = command;
    }
    /// <summary>
    ///     Registers a server command.Currently not in use.
    /// </summary>
    /// <param name="commandName">The name of the command.</param>
    /// <param name="command">The command Object to register.</param>
    public static void AddServerCommand(string commandName, Command command)
    {
        _ServerCommands[commandName] = command;
    }

    /// <summary>
    ///     Evaluates if the given message is a command and runs the relevant command.
    /// </summary>
    /// <param name="command">The message to evaluate.</param>
    public static void RunLocalCommand(string command)
    {
        ParsedCommand parsedCommand = ParseCommand(command.Substring(1)); //Parse command ignoring the first character which is the command indicator.
        string commandName = parsedCommand.Command.ToLower();
        if (_LocalCommands.ContainsKey(commandName))
        {
            ArchipelagoConsole.LogMessage(command);
            _LocalCommands[commandName].Run(parsedCommand.Args);
            return;
        }
        ArchipelagoConsole.LogMessage($"{commandName} is not a recognized command.");
    }
    /// <inheritdoc cref="RunLocalCommand(string)"/>
    public static void RunServerCommand(string command)
    {
        ParsedCommand parsedCommand = ParseCommand(command);
        string commandName = parsedCommand.Command.ToLower();

        if (_ServerCommands.ContainsKey(commandName))
        {
            _ServerCommands[commandName].Run(parsedCommand.Args);
            return;
        }
        ArchipelagoConsole.LogMessage($"{commandName} is not a recognized command.");
    }

    /// <summary>
    ///     Runs the help text command and outputs it to the console.
    /// </summary>
    public static void PrintHelpText()
    {
        string[] Keys = _LocalCommands.Keys.ToArray();
        foreach (string key in Keys)
        {
            if (key != "help")
            {
                ArchipelagoConsole.LogMessage("Name:\n\t" + System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(key));
                ArchipelagoConsole.LogMessage("Description:\n\t" + _LocalCommands[key].Description);
                ArchipelagoConsole.LogMessage(_LocalCommands[key].Syntax);
            }
        }
    }

    /// <summary>
    ///     Initializes all of the locally defined commands.
    /// </summary>
    public static void initializeLocalCommands()
    {
        _LocalCommands["room"] = new RoomCommand("Room");
        _LocalCommands["roompool"] = new RoomCommand("RoomPool"); // Alias for room command
        _LocalCommands["adjust"] = new AdjustCommand("Adjust");
        _LocalCommands["item"] = new ItemCommand("Item");
        _LocalCommands["help"] = new HelpCommand("Help");
        _LocalCommands["force"] = new ForceCommand("Force");
        _LocalCommands["sync"] = new SyncCommand("Sync"); // New sync command for Archipelago data
        _LocalCommands["debug"] = new DebugCommand("Debug"); // Debug command for investigating game systems
        _LocalCommands["received"] = new ReceivedCommand("Received"); // Show received Archipelago items
        _LocalCommands["resetdata"] = new ResetData("ResetData");
        _LocalCommands["collect"] = new CollectCommand("Collect"); // Collect location from the Archipelago item pool, for testing purposes.
        _LocalCommands["recordevent"] = new RecordEventCommand("RecordEvent"); // records an event to set some of the vanilla states
    }

    /// <summary>
    ///     Parses and breaks down a command into the command and it's arguements.
    /// </summary>
    /// <param name="command">The Command to Parse.</param>
    /// <returns>A ParsedCommand with the command and it's arguements.</returns>
    private static ParsedCommand ParseCommand(string command)
    {
        if (command.Length > 1)
        {
            bool quoteOpen = false;
            List<string> args = [];
            string curr = "";
            int count = 0;
            string commandName = "";
            foreach (char c in command)
            {
                if (c == '"')
                {
                    quoteOpen = !quoteOpen;
                }
                else if ((c == ' ') && !quoteOpen)
                {
                    if (count == 0)
                    {
                        commandName = curr;
                        count++;
                        curr = "";
                    }
                    else
                    {
                        args.Add(curr);
                        count++;
                        curr = "";
                    }

                }
                else
                {
                    curr += c;
                }
            }
            if (command.Length == curr.Length)
            {
                commandName = curr;
            }
            else if (curr.Length > 0)
            {
                args.Add(curr);
            }

            return new ParsedCommand(commandName, args);
        }
        return new ParsedCommand("", [""]);
    }
}

/// <summary>
///     The Command Framework.
/// </summary>
/// <param name="name">The name of the command.</param>
public abstract class Command(string name)
{
    public string Name = name;

    public abstract string Description
    {
        get;
    }
    public abstract string Syntax
    {
        get;
    }

    /// <summary>
    ///     The core functionality of the command.
    /// </summary>
    /// <param name="Args">The arguements for running the command.</param>
    public abstract void Run(List<string> Args);
}

/// <summary>
///     A command for manipulating the room pool.
/// </summary>
/// <param name="name">The name of the command.</param>
public class RoomCommand(string name) : Command(name)
{
    private readonly string _Description = "Manages the room pool - add, remove, list, or clear rooms";
    public override string Description
    {
        get { return _Description; }
    }
    private readonly string _Syntax = "Usage:\n\t/room add <RoomName> - Add a room to the pool\n\t/room remove <RoomName> - Remove a room from the pool\n\t/room list - List all rooms and their pool status\n\t/room list unlocked - List only unlocked rooms\n\t/room clear - Remove all non-vanilla rooms from pool\n\t/room clearall - Clear ALL rooms (for Archipelago mode)";
    public override string Syntax
    {
        get { return _Syntax; }
    }
    public override void Run(List<string> Args)
    {
        if (Args.Count < 1)
        {
            ArchipelagoConsole.LogMessage($"Error: No subcommand provided.\n{_Syntax}");
            return;
        }

        string subcommand = Args[0].ToLower();

        // List doesn't require being in a run
        if (subcommand == "list")
        {
            bool unlockedOnly = Args.Count > 1 && Args[1].ToLower() == "unlocked";
            ListRooms(unlockedOnly);
            return;
        }

        // Other commands require being in a run
        if (!ModInstance.IsInRun)
        {
            ArchipelagoConsole.LogMessage("You are not currently in a run. You can only modify the pool during a run.");
            return;
        }

        if (subcommand == "add")
        {
            if (Args.Count < 2)
            {
                ArchipelagoConsole.LogMessage("Error: No room name provided.\nUsage: /room add <RoomName>");
                return;
            }
            string roomName = string.Join(" ", Args.Skip(1));
            AddRoomToPool(roomName);
        }
        else if (subcommand == "remove")
        {
            if (Args.Count < 2)
            {
                ArchipelagoConsole.LogMessage("Error: No room name provided.\nUsage: /room remove <RoomName>");
                return;
            }
            string roomName = string.Join(" ", Args.Skip(1));
            RemoveRoomFromPool(roomName);
        }
        else if (subcommand == "clear")
        {
            ClearPool();
        }
        else if (subcommand == "clearall")
        {
            ClearAllForArchipelago();
        }
        else
        {
            ArchipelagoConsole.LogMessage($"Error: Unknown subcommand '{subcommand}'.\n{_Syntax}");
        }
    }

    /// <summary>
    ///     Prints out the mod details and counts of the current room pool.
    /// </summary>
    /// <param name="unlockedOnly">Whether to display only rooms that have been unlocked.</param>
    private void ListRooms(bool unlockedOnly)
    {
        var rooms = Plugin.ModRoomManager.Rooms;
        if (rooms == null || rooms.Count == 0)
        {
            ArchipelagoConsole.LogMessage("No rooms have been initialized yet.");
            return;
        }

        int unlockedCount = 0;
        int lockedCount = 0;
        int vanillaCount = 0;

        ArchipelagoConsole.LogMessage(unlockedOnly ? "=== Unlocked Rooms ===" : "=== All Rooms ===");
        foreach (var room in rooms)
        {
            if (room.IsUnlocked) unlockedCount++;
            else lockedCount++;
            if (room.UseVanilla) vanillaCount++;

            if (unlockedOnly && !room.IsUnlocked) continue;

            string status = room.IsUnlocked ? "[UNLOCKED]" : "[LOCKED]";
            string vanilla = room.UseVanilla ? " (Vanilla)" : " (AP Mode)";
            string poolInfo = $"Pool: {room.RoomsLeftInPool}/{room.RoomPoolCount}";
            ArchipelagoConsole.LogMessage($"  {status} {room.Name}{vanilla} - {poolInfo}");
        }
        ArchipelagoConsole.LogMessage($"Summary: {unlockedCount} unlocked, {lockedCount} locked, {vanillaCount} vanilla mode");
    }

    /// <summary>
    ///     Adds a Room to the room pool.
    /// </summary>
    /// <param name="roomName">The name of the room to add.</param>
    private void AddRoomToPool(string roomName)
    {
        ModRoom room = Plugin.ModRoomManager.GetRoomByName(roomName.ToUpper());
        if (room == null)
        {
            ArchipelagoConsole.LogMessage($"Error: '{roomName}' is not a valid room name.");
            return;
        }

        room.IsUnlocked = true;
        room.RoomPoolCount++;
        Plugin.ModRoomManager.UpdateRoomPools();
        ArchipelagoConsole.LogMessage($"Added '{room.Name}' to the pool. Pool count: {room.RoomPoolCount}");
    }

    /// <summary>
    ///     Removes a room from the current room pool.
    /// </summary>
    /// <param name="roomName">The name of the room to remove.</param>
    private void RemoveRoomFromPool(string roomName)
    {
        ModRoom room = Plugin.ModRoomManager.GetRoomByName(roomName.ToUpper());
        if (room == null)
        {
            ArchipelagoConsole.LogMessage($"Error: '{roomName}' is not a valid room name.");
            return;
        }

        if (!room.IsUnlocked)
        {
            ArchipelagoConsole.LogMessage($"'{room.Name}' is already not in the pool.");
            return;
        }

        room.IsUnlocked = false;
        Plugin.ModRoomManager.UpdateRoomPools();
        ArchipelagoConsole.LogMessage($"Removed '{room.Name}' from the pool.");
    }

    /// <summary>
    ///     Empties the current room pool.
    /// </summary>
    private void ClearPool()
    {
        Plugin.ModRoomManager.EmptyDraftPool();
        Plugin.ModRoomManager.UpdateRoomPools();
        ArchipelagoConsole.LogMessage("Cleared all non-vanilla rooms from the pool.");
    }

    /// <summary>
    ///     Clears all the rooms for archipelago then rebuilds the room pool based on received items and settings.
    /// </summary>
    private void ClearAllForArchipelago()
    {
        Plugin.ModRoomManager.ClearAllRoomsForArchipelago();
        if (ModInstance.IsInRun)
        {
            Plugin.ModRoomManager.UpdateRoomPools();
        }
        ArchipelagoConsole.LogMessage("Cleared ALL rooms and disabled vanilla mode for Archipelago.");
    }
}

/// <summary>
///     Adjusts various resource totals.
/// </summary>
/// <param name="name"></param>
public class AdjustCommand(string name) : Command(name)
{
    private string _Description = "Allows you to Adjust the ammount of certain run resources";
    public override string Description
    {
        get { return _Description; }
    }
    private string _Syntax = "Usage:\n\t/Adjust Gems <Adjustment_Amount>\n\t/Adjust Keys <Adjustment_Amount>\n\t/Adjust Dice <Adjustment_Amount>\n\t/Adjust Stars <Adjustment_Amount>\n\t/Adjust Steps <Adjustment_Amount>\n\t/Adjust Gold <Adjustment_Amount>\n\t/Adjust Luck <Adjustment_Amount>";
    public override string Syntax
    {
        get { return _Syntax; }
    }

    public override void Run(List<string> Args)
    {
        ArchipelagoConsole.LogMessage(Args.Join(" "));
        if (!ModInstance.IsInRun)
        {
            ArchipelagoConsole.LogMessage("You are not currently in a run, you can only run this command during a run.");
            return;
        }
        if (Args.Count == 2)
        {
            string subcommand = Args[0];
            if (subcommand.ToLower() == "gems")
            {
                try
                {
                    int count = int.Parse(Args[1]);
                    ModInstance.GemManager.FindIntVariable("Gem Adjustment Amount").Value = count;
                    ModInstance.GemManager.SendEvent("Update with Sound");
                    ArchipelagoConsole.LogMessage($"Adjusted Gems by {count}.");
                    return;
                }
                catch
                {
                    ArchipelagoConsole.LogMessage($"Error Running Command {Name} {subcommand}: {Args[1]} is not a valid integer.");
                    return;
                }

            }
            else if (subcommand.ToLower() == "gold")
            {
                try
                {
                    int count = int.Parse(Args[1]);
                    ModInstance.GoldManager.FindIntVariable("Adjustment Amount").Value = count;
                    ModInstance.GoldManager.SendEvent("Update");
                    ArchipelagoConsole.LogMessage($"Adjusted Gold by {count}.");
                    return;
                }
                catch
                {
                    ArchipelagoConsole.LogMessage($"Error Running Command {Name} {subcommand}: {Args[1]} is not a valid integer.");
                    return;
                }

            }
            else if (subcommand.ToLower() == "steps")
            {
                try
                {
                    int count = int.Parse(Args[1]);
                    ModInstance.StepManager.FindIntVariable("Adjustment Amount").Value = count;
                    ModInstance.StepManager.SendEvent("Update");
                    ArchipelagoConsole.LogMessage($"Adjusted Steps by {count}.");
                    return;
                }
                catch
                {
                    ArchipelagoConsole.LogMessage($"Error Running Command {Name} {subcommand}: {Args[1]} is not a valid integer.");
                    return;
                }

            }
            else if (subcommand.ToLower() == "dice")
            {
                try
                {
                    int count = int.Parse(Args[1]);
                    ModInstance.DiceManager.FindIntVariable("Adjustment Amount").Value = count;
                    ModInstance.DiceManager.SendEvent("Update");
                    ArchipelagoConsole.LogMessage($"Adjusted Dice by {count}.");
                    return;
                }
                catch
                {
                    ArchipelagoConsole.LogMessage($"Error Running Command {Name} {subcommand}: {Args[1]} is not a valid integer.");
                    return;
                }

            }
            else if (subcommand.ToLower() == "keys")
            {
                try
                {
                    int count = int.Parse(Args[1]);
                    ModInstance.KeyManager.FindIntVariable("Adjustment Amount").Value = count;
                    ModInstance.KeyManager.SendEvent("Update");
                    ArchipelagoConsole.LogMessage($"Adjusted Keys by {count}.");
                    return;
                }
                catch
                {
                    ArchipelagoConsole.LogMessage($"Error Running Command {Name} {subcommand}: {Args[1]} is not a valid integer.");
                    return;
                }

            }
            else if (subcommand.ToLower() == "stars")
            {
                try
                {
                    int count = int.Parse(Args[1]);
                    int totalStars = ModInstance.StarManager.FindIntVariable("TotalStars").Value;
                    if (totalStars + count > 0)
                    {
                        ModInstance.StarManager.FindIntVariable("TotalStars").Value = totalStars + count;

                    }
                    else
                    {
                        ModInstance.StarManager.FindIntVariable("TotalStars").Value = 0;
                    }
                    ArchipelagoConsole.LogMessage($"Adjusted Stars by {count}.");
                    return;
                }
                catch
                {
                    ArchipelagoConsole.LogMessage($"Error Running Command {Name} {subcommand}: {Args[1]} is not a valid integer.");
                    return;
                }

            }
            else if (subcommand.ToLower() == "luck")
            {
                try
                {
                    int count = int.Parse(Args[1]);
                    int luck = ModInstance.LuckManager.FindIntVariable("LUCK").Value;
                    if (luck + count > 0)
                    {
                        ModInstance.LuckManager.FindIntVariable("LUCK").Value = luck + count;

                    }
                    else
                    {
                        ModInstance.LuckManager.FindIntVariable("LUCK").Value = 0;
                    }
                    ArchipelagoConsole.LogMessage($"Adjusted Luck by {count}.");
                    return;
                }
                catch
                {
                    ArchipelagoConsole.LogMessage($"Error Running Command {Name} {subcommand}: {Args[1]} is not a valid integer.");
                    return;
                }

            }
            else if (subcommand.ToLower() == "allowance")
            {
                try
                {
                    int count = int.Parse(Args[1]);

                    GameObject.Find("DAY").GetComponent<PlayMakerFSM>().FindIntVariable("allowance").Value += count;
                    return;
                }
                catch (Exception ex)
                {
                    ArchipelagoConsole.LogMessage(ex.Message);
                    Logging.Log(ex, "Items");
                    return;
                }
            }
            ArchipelagoConsole.LogMessage($"Error Running Command {Name}: invalid subcommand {subcommand}");
            return;
        }
        ArchipelagoConsole.LogMessage($"Error Running Command {Name}: no parameters provided.");
    }
}
/// <summary>
///     A command for manipulating items in the inventory.
/// </summary>
/// <param name="name">The name of the command.</param>
public class ItemCommand(string name) : Command(name)
{
    private string _Description = "Adds or Removes Items from the inventory.";
    public override string Description
    {
        get { return _Description; }
    }
    private string _Syntax = "Usage\n/Item Add <Item>\n/Item Remove <Item>\n/Item List <prespawn|estateitems|pickedup|coatcheck|useditems>";
    public override string Syntax
    {
        get { return _Syntax; }
    }
    public override void Run(List<string> Args)
    {
        if (!ModInstance.IsInRun)
        {
            ArchipelagoConsole.LogMessage("You are not currently in a run, you can only run this command during a run.");
            return;
        }
        if (Args.Count > 1)
        {
            string subcommand = Args[0];
            if (subcommand.ToLower() == "list")
            {
                ArchipelagoConsole.LogMessage($"Item List\n{Plugin.ModItemManager.ListItems(Args[1])}");
                return;
            }
            else if (subcommand.ToLower() == "add")
            {
                string itemName = Args[1];
                for (int i = 2; i < Args.Count; i++)
                {
                    itemName += " " + Args[i];
                }

                ArchipelagoConsole.LogMessage($"Attemping to add item {itemName}");

                GameObject item = Plugin.ModItemManager.GetInventoryItem(itemName);
                
                //Handle items that don't start in the prespawn pool.
                if (item == null)
                {
                    string iconName = Plugin.UniqueItemManager.GetIconName("UPGRADE DISK");
                    GameObject InventoryGO = GameObject.Find("UI OVERLAY CAM/MENU/Blue Print /Inventory");
                    PlayMakerFSM Inventory = InventoryGO.GetFsm("Inventory Icons");
                    PlayMakerArrayListProxy iconList = GameObject.Find("UI OVERLAY CAM/MENU/Blue Print /Inventory/InventoryIcons").GetComponent<PlayMakerArrayListProxy>();
                    PlayMakerArrayListProxy InventoryIcons = InventoryGO.GetArrayListProxy("Inventory Icons");
                    GameObject icon = null;
                    foreach (var invIcon in iconList.arrayList)
                    {
                        GameObject iconGo = invIcon.TryCast<GameObject>();
                        if (iconGo != null)
                        {
                            if (iconGo.name.Contains(iconName))
                            {
                                icon = iconGo;
                            }
                        }
                    }
                    if (icon != null && InventoryIcons != null)
                    {
                        InventoryIcons.Add(icon, "GameObject");
                        ModItemManager.PickedUp.Add(item, "GameObject");
                        //Send Event 0 to the Global Manager.
                        Inventory.SendEvent("Update");
                    }
                    ArchipelagoConsole.LogMessage($"Error Running Command {Name} {subcommand}: {itemName} is not a valid Item Name");
                    return; 
                }
                else {
                    // Check PreSpawn EstateItems, PickedUp, CoatCheck, UsedItems
                    if (Plugin.ModItemManager.IsItemSpawnable(item) || true)
                    {
                        GameObject InventoryGO = GameObject.Find("UI OVERLAY CAM/MENU/Blue Print /Inventory");
                        PlayMakerArrayListProxy InventoryIcons = InventoryGO.GetArrayListProxy("Inventory Icons");
                        GameObject icon = Plugin.UniqueItemManager.GetIconGameObject(item.name);

                        if (icon != null && InventoryIcons != null)
                        {
                            if (!ModItemManager.PickedUp.Contains(item.name))
                            {
                                ModItemManager.PickedUp.Add(item, "GameObject");
                                
                            }
                            InventoryIcons.Add(icon, "GameObject");

                            if (itemName == "RUNNING SHOES")
                            {
                                ModInstance.RunningEngine.SendEvent("Update");
                                
                            }
                            //Send Event 0 to the Global Manager.
                            return;
                        }
                        ArchipelagoConsole.LogMessage($"Error Running Command {Name} {subcommand}: {itemName} is not a valid Item Name");
                        return;
                    }
                }
                ArchipelagoConsole.LogMessage($"Error Running Command {Name} {subcommand}: {itemName} Can't be added to inventory.");
                return;
            }
            else if (subcommand.ToLower() == "remove")
            {
                string itemName = "";
                for (int i = 1; i < Args.Count; i++)
                {
                    itemName += Args[i];
                }
                GameObject item = Plugin.ModItemManager.GetPickedUpItem(itemName);
                if (item == null)
                {
                    ArchipelagoConsole.LogMessage($"Error Running Command {Name} {subcommand}: {itemName} is not a valid Item Name or is not in your Inventory");
                    return;
                }
                string iconName = Plugin.UniqueItemManager.GetIconName(itemName);
                GameObject InventoryGO = GameObject.Find("UI OVERLAY CAM/MENU/Blue Print /Inventory");
                PlayMakerFSM Inventory = InventoryGO.GetFsm("Inventory Icons");
                PlayMakerArrayListProxy InventoryIcons = InventoryGO.GetArrayListProxy("Inventory Icons");
                GameObject icon = Plugin.UniqueItemManager.GetIconGameObject(iconName);

                Logging.LogWarning(icon != null);
                Logging.LogWarning(InventoryIcons != null);
                if (icon != null && InventoryIcons != null)
                {
                    if (!ModItemManager.PickedUp.Contains(Name))
                    {
                        ModItemManager.PickedUp.Add(item, "GameObject");
                    }
                    InventoryIcons.Add(icon, "GameObject");

                    if (itemName == "RUNNING SHOES")
                    {
                        ModInstance.RunningEngine.SendEvent("Update");
                    }
                    //Send Event 0 to the Global Manager.
                }

                ArchipelagoConsole.LogMessage($"Error Running Command {Name} {subcommand}: {itemName} can't be removed from inventory.");
                return;

            }
            ArchipelagoConsole.LogMessage($"Error Running Command {Name}: invalid subcommand {subcommand}");
            return;
        }
        else
            ArchipelagoConsole.LogMessage($"Error Running Command {Name}: no parameters provided.");
    }
}

/// <summary>
///     A command for displaying all of the commands and how to use them.
/// </summary>
/// <param name="name">The name of the command.</param>
public class HelpCommand(string name) : Command(name)
{
    private string _Description = "Displays all Local Commands";
    public override string Description
    {
        get { return _Description; }
    }
    private readonly string _Syntax = "Usage\n\t/Help";
    public override string Syntax
    {
        get { return _Syntax; }
    }
    public override void Run(List<string> Args)
    {
        CommandManager.PrintHelpText();
    }
}

/// <summary>
///     A command for forcing a room to appear in drafting when next logically possible.
/// </summary>
/// <param name="name">The name of the command.</param>
public class ForceCommand(string name) : Command(name)
{
    private readonly string _Description = "Forces a draft of the room when next possible";
    public override string Description
    {
        get { return _Description; }
    }
    private readonly string _Syntax = "Usage\n\t/Force <Room>\n\t/Force <Room>";
    public override string Syntax
    {
        get { return _Syntax; }
    }
    public override void Run(List<string> Args)
    {
        string roomName = string.Join(" ", Args);
        ModRoom room = Plugin.ModRoomManager.GetRoomByName(roomName);
        if (room != null)
        {
            ModRoomManager.ForceRoomQueue.Add(room);
        }
    }
}

/// <summary>
///     A command for resyncing the room pool with the received item pool.
/// </summary>
/// <param name="name">The name of the command.</param>
public class SyncCommand(string name) : Command(name)
{
    private readonly string _Description = "Syncs room pool with Archipelago received items";
    public override string Description
    {
        get { return _Description; }
    }
    private readonly string _Syntax = "Usage:\n\t/sync rooms - Sync room pool from Archipelago received items\n\t/sync status - Show sync status";
    public override string Syntax
    {
        get { return _Syntax; }
    }

    public override void Run(List<string> Args)
    {
        if (Args.Count < 1)
        {
            ArchipelagoConsole.LogMessage($"Error: No subcommand provided.\n{_Syntax}");
            return;
        }

        string subcommand = Args[0].ToLower();

        if (subcommand == "rooms")
        {
            SyncRoomsFromArchipelago();
        }
        else if (subcommand == "status")
        {
            ShowSyncStatus();
        }
        else
        {
            ArchipelagoConsole.LogMessage($"Error: Unknown subcommand '{subcommand}'.\n{_Syntax}");
        }
    }
    /// <summary>
    ///     A function to rebuild the roompool to match the received rooms from archipelago.
    /// </summary>
    private void SyncRoomsFromArchipelago()
    {
        if (!ArchipelagoClient.Authenticated)
        {
            ArchipelagoConsole.LogMessage("Error: Not connected to Archipelago. Please connect first.");
            return;
        }

        if (!ModInstance.HasInitializedRooms)
        {
            ArchipelagoConsole.LogMessage("Error: Rooms have not been initialized yet. Start a run first.");
            return;
        }

        // Check if RoomDraftSanity is enabled
        if (!ArchipelagoOptions.RoomDraftSanity)
        {
            ArchipelagoConsole.LogMessage("RoomDraftSanity is disabled in your Archipelago options.");
            ArchipelagoConsole.LogMessage("Room drafts will use vanilla behavior. No sync needed.");
            return;
        }

        var receivedItems = ArchipelagoClient.ServerData.ReceivedItems;

        // Re-load arrays first to ensure we have fresh references
        ModInstance.ReloadArrays();

        // First, clear ALL rooms for Archipelago mode (disables vanilla handling too)
        Plugin.ModRoomManager.ClearAllRoomsForArchipelago();

        int syncedCount = 0;
        int skippedCount = 0;

        // Then unlock rooms that are in received items
        if (receivedItems != null && receivedItems.Count > 0)
        {
            foreach (string itemName in receivedItems)
            {
                if (Plugin.ModRoomManager.UnlockRoomForArchipelago(itemName))
                {
                    syncedCount++;
                }
                else
                {
                    // Item is not a room, skip it
                    skippedCount++;
                }
            }
        }

        // Update the pools after sync
        Plugin.ModRoomManager.UpdateRoomPools();

        ArchipelagoConsole.LogMessage($"Room sync complete: {syncedCount} rooms unlocked, {skippedCount} non-room items skipped.");
        ArchipelagoConsole.LogMessage("All rooms set to Archipelago mode (vanilla handling disabled).");
    }

    /// <summary>
    ///     Displays the current information on what data has been received from archipelago.
    /// </summary>
    private void ShowSyncStatus()
    {
        if (!ArchipelagoClient.Authenticated)
        {
            ArchipelagoConsole.LogMessage("Status: Not connected to Archipelago");
            return;
        }

        var receivedItems = ArchipelagoClient.ServerData.ReceivedItems;
        int receivedRoomCount = 0;
        int unlockedRoomCount = 0;

        // Count received rooms
        if (receivedItems != null)
        {
            foreach (string itemName in receivedItems)
            {
                if (Plugin.ModRoomManager.GetRoomByName(itemName.ToUpper()) != null)
                {
                    receivedRoomCount++;
                }
            }
        }

        // Count unlocked rooms
        foreach (var room in Plugin.ModRoomManager.Rooms)
        {
            if (room.IsUnlocked && !room.UseVanilla)
            {
                unlockedRoomCount++;
            }
        }

        ArchipelagoConsole.LogMessage($"=== Sync Status ===");
        ArchipelagoConsole.LogMessage($"Connected: Yes");
        ArchipelagoConsole.LogMessage($"Received room items: {receivedRoomCount}");
        ArchipelagoConsole.LogMessage($"Currently unlocked (non-vanilla): {unlockedRoomCount}");
        ArchipelagoConsole.LogMessage($"Total items received: {receivedItems?.Count ?? 0}");
    }
}

/// <summary>
///     A data structure for containing the parsed command name and it's arguements.
/// </summary>
public class ParsedCommand
{
    public string Command;
    public List<string> Args;
    public ParsedCommand(string command, List<string> args)
    {
        Command = command;
        Args = args;
    }
}

/// <summary>
///     A command for listing items received from archipelago.
/// </summary>
/// <param name="name">The name of the command.</param>
public class ReceivedCommand(string name) : Command(name)
{
    private readonly string _Description = "Lists items received from Archipelago";
    public override string Description
    {
        get { return _Description; }
    }
    private readonly string _Syntax = "Usage:\n\t/received - List all received items\n\t/received rooms - List only received rooms\n\t/received items - List only received non-room items\n\t/received count - Show counts by category";
    public override string Syntax
    {
        get { return _Syntax; }
    }

    public override void Run(List<string> Args)
    {
        if (!ArchipelagoClient.Authenticated)
        {
            ArchipelagoConsole.LogMessage("Not connected to Archipelago.");
            return;
        }

        var receivedItems = ArchipelagoClient.ServerData.ReceivedItems;
        if (receivedItems == null || receivedItems.Count == 0)
        {
            ArchipelagoConsole.LogMessage("No items received from Archipelago yet.");
            return;
        }

        string subcommand = Args.Count > 0 ? Args[1].ToLower() : "all";

        if (subcommand == "rooms")
        {
            ListReceivedRooms(receivedItems);
        }
        else if (subcommand == "items")
        {
            ListReceivedNonRooms(receivedItems);
        }
        else if (subcommand == "count")
        {
            ShowCounts(receivedItems);
        }
        else
        {
            ListAll(receivedItems);
        }
    }

    /// <summary>
    ///     Outputs the received items to the console.
    /// </summary>
    /// <param name="receivedItems">A list of items received from Archipelago.</param>
    private void ListReceivedRooms(List<string> receivedItems)
    {
        var rooms = receivedItems.Where(i => Plugin.ModRoomManager.GetRoomByName(i.ToUpper()) != null).ToList();
        ArchipelagoConsole.LogMessage($"=== Received Rooms ({rooms.Count}) ===");
        foreach (var room in rooms)
        {
            ModRoom modRoom = Plugin.ModRoomManager.GetRoomByName(room.ToUpper());
            string poolInfo = modRoom != null ? $" [Pool: {modRoom.RoomsLeftInPool}/{modRoom.RoomPoolCount}]" : "";
            ArchipelagoConsole.LogMessage($"  {room}{poolInfo}");
        }
    }

    /// <summary>
    ///     A list of items received from archipelago excluding rooms.
    /// </summary>
    /// <param name="receivedItems"></param>
    private void ListReceivedNonRooms(List<string> receivedItems)
    {
        var nonRooms = receivedItems.Where(i => Plugin.ModRoomManager.GetRoomByName(i.ToUpper()) == null).ToList();
        ArchipelagoConsole.LogMessage($"=== Received Non-Room Items ({nonRooms.Count}) ===");
        foreach (var item in nonRooms)
        {
            string type = Plugin.ModItemManager.GetItemType(item) ?? "Unknown";
            ArchipelagoConsole.LogMessage($"  [{type}] {item}");
        }
    }

    /// <summary>
    ///     Lists the counts of certain types of items.
    /// </summary>
    /// <param name="receivedItems">The list of received items.</param>
    private void ShowCounts(List<string> receivedItems)
    {
        int roomCount = 0;
        int permanentCount = 0;
        int junkCount = 0;
        int unknownCount = 0;

        foreach (var item in receivedItems)
        {
            if (Plugin.ModRoomManager.GetRoomByName(item.ToUpper()) != null)
            {
                roomCount++;
            }
            else
            {
                string type = Plugin.ModItemManager.GetItemType(item);
                if (type == "Permanent") permanentCount++;
                else if (type == "Junk") junkCount++;
                else unknownCount++;
            }
        }

        ArchipelagoConsole.LogMessage($"=== Received Item Counts ===");
        ArchipelagoConsole.LogMessage($"  Rooms:     {roomCount}");
        ArchipelagoConsole.LogMessage($"  Permanent: {permanentCount}");
        ArchipelagoConsole.LogMessage($"  Junk:      {junkCount}");
        ArchipelagoConsole.LogMessage($"  Unknown:   {unknownCount}");
        ArchipelagoConsole.LogMessage($"  Total:     {receivedItems.Count}");
    }

    /// <summary>
    ///     Lists the data of all received items.
    /// </summary>
    /// <param name="receivedItems">The list of received items.</param>
    private void ListAll(List<string> receivedItems)
    {
        ArchipelagoConsole.LogMessage($"=== All Received Items ({receivedItems.Count}) ===");
        foreach (var item in receivedItems)
        {
            bool isRoom = Plugin.ModRoomManager.GetRoomByName(item.ToUpper()) != null;
            string type = isRoom ? "Room" : (Plugin.ModItemManager.GetItemType(item) ?? "Unknown");
            ArchipelagoConsole.LogMessage($"  [{type}] {item}");
        }
    }
}



/// <summary>
///     Debug command to investigate game systems like FSMs, draft pools, and the Entrance Hall.
/// </summary>
public class DebugCommand(string name) : Command(name)
{
    private readonly string _Description = "Debug tools to investigate game systems";
    public override string Description
    {
        get { return _Description; }
    }
    private readonly string _Syntax = "Usage:\n\t/debug entrance - Investigate Entrance Hall FSM\n\t/debug arrays - List all picker arrays\n\t/debug pool <ArrayName> - List rooms in array with status\n\t/debug poolstatus - Check POOL REMOVAL for all rooms\n\t/debug fsm <path> - Inspect FSM at path\n\t/debug grid - Show current grid/draft info";
    public override string Syntax
    {
        get { return _Syntax; }
    }

    public override void Run(List<string> Args)
    {
        if (Args.Count < 1)
        {
            ArchipelagoConsole.LogMessage($"Error: No subcommand provided.\n{_Syntax}");
            return;
        }

        string subcommand = Args[0].ToLower();

        if (subcommand == "entrance")
        {
            InvestigateEntranceHall();
        }
        else if (subcommand == "arrays")
        {
            ListPickerArrays();
        }
        else if (subcommand == "grid")
        {
            ShowGridInfo();
        }
        else if (subcommand == "fsm" && Args.Count > 1)
        {
            string path = string.Join(" ", Args.Skip(1));
            InspectFSM(path);
        }
        else if (subcommand == "pool" && Args.Count > 1)
        {
            string arrayName = string.Join(" ", Args.Skip(1));
            InspectPoolArray(arrayName);
        }
        else if (subcommand == "poolstatus")
        {
            CheckAllPoolRemoval();
        }
        else
        {
            ArchipelagoConsole.LogMessage($"Error: Unknown subcommand '{subcommand}'.\n{_Syntax}");
        }
    }

    /// <summary>
    ///     A function for outputting data about the Entrance Hall to the console.
    /// </summary>
    private void InvestigateEntranceHall()
    {
        ArchipelagoConsole.LogMessage("=== Investigating Entrance Hall Draft System ===");

        // Look for the Entrance Hall room engine
        GameObject entranceEngine = GameObject.Find("__SYSTEM/The Room Engines/ENTRANCE HALL");
        if (entranceEngine != null)
        {
            ArchipelagoConsole.LogMessage($"Found Entrance Hall engine at: {entranceEngine.name}");

            // List all FSMs on this object
            var fsms = entranceEngine.GetComponents<PlayMakerFSM>();
            foreach (var fsm in fsms)
            {
                ArchipelagoConsole.LogMessage($"  FSM: {fsm.FsmName}");

                // Look for relevant variables
                foreach (var boolVar in fsm.FsmVariables.BoolVariables)
                {
                    if (boolVar.Name.ToUpper().Contains("POOL") || boolVar.Name.ToUpper().Contains("DRAFT"))
                    {
                        ArchipelagoConsole.LogMessage($"    Bool: {boolVar.Name} = {boolVar.Value}");
                    }
                }
            }
        }
        else
        {
            ArchipelagoConsole.LogMessage("Entrance Hall engine not found!");
        }

        // Look for Entrance Hall specific picker/draft components
        GameObject planPicker = GameObject.Find("__SYSTEM/THE DRAFT/PLAN PICKER");
        if (planPicker != null)
        {
            ArchipelagoConsole.LogMessage($"\nPlan Picker children count: {planPicker.transform.childCount}");

            // Look for anything with "Entrance" or "Hall" in the name
            for (int i = 0; i < planPicker.transform.childCount; i++)
            {
                var child = planPicker.transform.GetChild(i);
                string childName = child.name.ToUpper();
                if (childName.Contains("ENTRANCE") || childName.Contains("HALL") || childName.Contains("FRONT") || childName.Contains("FIRST"))
                {
                    ArchipelagoConsole.LogMessage($"  [{i}] {child.name}");
                    var proxy = child.GetComponent<PlayMakerArrayListProxy>();
                    if (proxy != null)
                    {
                        ArchipelagoConsole.LogMessage($"       Array count: {proxy.GetCount()}");
                    }
                }
            }
        }

        // Look for "Entrance Draft" or similar GameObjects
        string[] searchPaths = [
            "__SYSTEM/THE DRAFT/ENTRANCE",
            "__SYSTEM/THE DRAFT/ENTRANCE HALL",
            "__SYSTEM/THE DRAFT/FRONT DOOR",
            "__SYSTEM/THE DRAFT/PLAN PICKER/ENTRANCE",
            "__SYSTEM/THE DRAFT/PLAN PICKER/FRONT"
        ];

        foreach (var path in searchPaths)
        {
            var obj = GameObject.Find(path);
            if (obj != null)
            {
                ArchipelagoConsole.LogMessage($"\nFound: {path}");
                var fsms = obj.GetComponents<PlayMakerFSM>();
                foreach (var fsm in fsms)
                {
                    ArchipelagoConsole.LogMessage($"  FSM: {fsm.FsmName}");
                }
            }
        }

        // Check the Grid for current draft info
        if (ModInstance.TheGrid != null)
        {
            ArchipelagoConsole.LogMessage($"\nGrid Variables:");
            var planPickVar = ModInstance.TheGrid.GetGameObjectVariable("theplanpick");
            if (planPickVar != null && planPickVar.Value != null)
            {
                ArchipelagoConsole.LogMessage($"  Current plan picker: {planPickVar.Value.name}");
            }

            var currentRoom = ModInstance.TheGrid.GetStringVariable("CURRENT ROOM");
            if (currentRoom != null)
            {
                ArchipelagoConsole.LogMessage($"  Current room: {currentRoom.Value}");
            }
        }
    }

    /// <summary>
    ///     Outputs information about picker arrays to the console.
    /// </summary>
    private void ListPickerArrays()
    {
        ArchipelagoConsole.LogMessage("=== All Picker Arrays ===");

        if (ModInstance.PickerDict == null || ModInstance.PickerDict.Count == 0)
        {
            ArchipelagoConsole.LogMessage("No picker arrays loaded.");
            return;
        }

        foreach (var kvp in ModInstance.PickerDict)
        {
            int count = kvp.Value?.GetCount() ?? 0;
            ArchipelagoConsole.LogMessage($"  {kvp.Key}: {count} rooms");
        }

        // Also list all children of Plan Picker to find any we might have missed
        GameObject planPicker = GameObject.Find("__SYSTEM/THE DRAFT/PLAN PICKER");
        if (planPicker != null)
        {
            ArchipelagoConsole.LogMessage($"\nAll Plan Picker children ({planPicker.transform.childCount} total):");
            for (int i = 0; i < Mathf.Min(planPicker.transform.childCount, 65); i++)
            {
                var child = planPicker.transform.GetChild(i);
                var proxy = child.GetComponent<PlayMakerArrayListProxy>();
                string proxyInfo = proxy != null ? $" [Array: {proxy.GetCount()}]" : "";

                // Check if this is in our PickerDict
                bool tracked = ModInstance.PickerDict.ContainsKey(child.name.Trim());
                string trackedInfo = tracked ? "" : " *NOT TRACKED*";

                ArchipelagoConsole.LogMessage($"  [{i}] {child.name}{proxyInfo}{trackedInfo}");
            }
        }
    }

    /// <summary>
    ///     Outputs information about The Grid to the console.
    /// </summary>
    private void ShowGridInfo()
    {
        ArchipelagoConsole.LogMessage("=== Grid/Draft Info ===");

        if (ModInstance.TheGrid == null)
        {
            ArchipelagoConsole.LogMessage("Grid not initialized.");
            return;
        }

        // List all variables
        foreach (var strVar in ModInstance.TheGrid.FsmVariables.StringVariables)
        {
            ArchipelagoConsole.LogMessage($"  String: {strVar.Name} = {strVar.Value}");
        }

        foreach (var boolVar in ModInstance.TheGrid.FsmVariables.BoolVariables)
        {
            ArchipelagoConsole.LogMessage($"  Bool: {boolVar.Name} = {boolVar.Value}");
        }

        foreach (var goVar in ModInstance.TheGrid.FsmVariables.GameObjectVariables)
        {
            string goName = goVar.Value != null ? goVar.Value.name : "null";
            ArchipelagoConsole.LogMessage($"  GameObject: {goVar.Name} = {goName}");
        }
    }

    /// <summary>
    ///     Outputs details about a particular FSM.
    /// </summary>
    /// <param name="path">The string path to the game object containing the FSM to inspect.</param>
    private void InspectFSM(string path)
    {
        GameObject obj = GameObject.Find(path);
        if (obj == null)
        {
            ArchipelagoConsole.LogMessage($"GameObject not found at: {path}");
            return;
        }

        ArchipelagoConsole.LogMessage($"=== FSMs at {path} ===");

        var fsms = obj.GetComponents<PlayMakerFSM>();
        if (fsms.Length == 0)
        {
            ArchipelagoConsole.LogMessage("No FSMs found on this object.");
            return;
        }

        foreach (var fsm in fsms)
        {
            ArchipelagoConsole.LogMessage($"\nFSM: {fsm.FsmName}");
            ArchipelagoConsole.LogMessage($"  Active State: {fsm.ActiveStateName}");
            ArchipelagoConsole.LogMessage($"  States ({fsm.FsmStates.Length}):");

            foreach (var state in fsm.FsmStates)
            {
                ArchipelagoConsole.LogMessage($"    - {state.Name}");
            }

            ArchipelagoConsole.LogMessage($"  Global Transitions:");
            foreach (var trans in fsm.FsmGlobalTransitions)
            {
                ArchipelagoConsole.LogMessage($"    - {trans.EventName} -> {trans.ToState}");
            }
        }
    }

    /// <summary>
    ///     Lists all rooms in a specific picker array and checks their POOL REMOVAL status.
    ///     Usage: /debug pool "FRONT - Tier 1"
    /// </summary>
    public void InspectPoolArray(string arrayName)
    {
        if (!ModInstance.PickerDict.ContainsKey(arrayName))
        {
            ArchipelagoConsole.LogMessage($"Array '{arrayName}' not found in PickerDict.");
            ArchipelagoConsole.LogMessage("Available arrays:");
            foreach (var key in ModInstance.PickerDict.Keys.Take(20))
            {
                ArchipelagoConsole.LogMessage($"  - {key}");
            }
            return;
        }

        var array = ModInstance.PickerDict[arrayName];
        ArchipelagoConsole.LogMessage($"=== Pool Array: {arrayName} ({array.GetCount()} rooms) ===");

        for (int i = 0; i < array.GetCount(); i++)
        {
            var roomObj = array.arrayList[i].TryCast<GameObject>();
            if (roomObj != null)
            {
                string roomName = roomObj.name;

                // Check the room's POOL REMOVAL status
                string poolRemovalStatus = "?";
                var roomEngine = GameObject.Find("__SYSTEM/The Room Engines/" + roomName);
                if (roomEngine != null)
                {
                    var fsm = roomEngine.GetFsm(roomName);
                    if (fsm != null)
                    {
                        var poolRemoval = fsm.GetBoolVariable("POOL REMOVAL");
                        if (poolRemoval != null)
                        {
                            poolRemovalStatus = poolRemoval.Value ? "REMOVED" : "AVAILABLE";
                        }
                    }
                }

                // Check our ModRoom status
                var modRoom = Plugin.ModRoomManager.GetRoomByName(roomName);
                string modStatus = modRoom != null ? (modRoom.IsUnlocked ? "Unlocked" : "Locked") : "Not tracked";

                ArchipelagoConsole.LogMessage($"  [{i}] {roomName} - FSM:{poolRemovalStatus}, Mod:{modStatus}");
            }
        }
    }

    /// <summary>
    ///     Check POOL REMOVAL status for all room engines.
    /// </summary>
    public void CheckAllPoolRemoval()
    {
        ArchipelagoConsole.LogMessage("=== Checking POOL REMOVAL for All Room Engines ===");

        var roomEngines = GameObject.Find("__SYSTEM/The Room Engines");
        if (roomEngines == null)
        {
            ArchipelagoConsole.LogMessage("Room Engines not found!");
            return;
        }

        int available = 0;
        int removed = 0;
        int unknown = 0;

        for (int i = 0; i < roomEngines.transform.childCount; i++)
        {
            var child = roomEngines.transform.GetChild(i);
            var fsm = child.GetComponent<PlayMakerFSM>();

            if (fsm != null)
            {
                var poolRemoval = fsm.GetBoolVariable("POOL REMOVAL");
                if (poolRemoval != null)
                {
                    if (poolRemoval.Value)
                    {
                        removed++;
                        // Only log removed rooms (to keep output manageable)
                        // ArchipelagoConsole.LogMessage($"  REMOVED: {child.name}");
                    }
                    else
                    {
                        available++;
                    }
                }
                else
                {
                    unknown++;
                }
            }
        }

        ArchipelagoConsole.LogMessage($"Summary: {available} available, {removed} removed, {unknown} unknown");
        ArchipelagoConsole.LogMessage($"Total room engines: {roomEngines.transform.childCount}");
    }
}

/// <summary>
///     A command for collecting a location from the archipelago item pool (for testing purposes). 
/// </summary>
/// <param name="name"></param>
public class CollectCommand(string name) : Command(name)
{
    public override string Description => "Collects a location from the Archipelago item pool (for testing purposes).";

    public override string Syntax => "Usage:\n\t/collect <LocationName>\n\nExample:\n\t/collect Closet First Entering";

    public override void Run(List<string> Args)
    {
        var locationName = string.Join(" ", Args);
        if (locationName.StartsWith("\"") && locationName.EndsWith("\""))
            locationName = locationName[1..^1];

        if (locationName == "goal")
        {
            Plugin.ArchipelagoClient.GoalCompleted();
            return;
        }

        if (locationName == "death")
        {
            DeathLinkHandler.ForceKillPlayer("KillPlayer called from console.");
            return;
        }

        ModInstance.ModEventHandler.OnOtherLocation(locationName);
    }
}

/// <summary>
///     A command for reseting the cached and stored data about the current run.
/// </summary>
/// <param name="name">The name of the Command.</param>
public class ResetData(string name) : Command(name)
{
    public override string Description => "Resets the stored data so a new run can be properly started.";

    public override string Syntax => "Usage:\n\t/ResetData";

    public override void Run(List<string> Args)
    {
        State.Reset();
        State.Initialize();
    }
}

/// <summary>
///     A Command for simulating an in game event for testing permanent unlocks.
/// </summary>
/// <param name="name">The name of the Command.</param>
public class RecordEventCommand(string name) : Command(name)
{
    public override string Description => "Records an event to set some of the vanilla states (for testing purposes).";

    public override string Syntax => "Usage:\n\t/RecordEvent <EventName>\n\nExample:\n\t/RecordEvent Orchard_Unlocked";

    public override void Run(List<string> Args)
    {
        var eventName = string.Join(" ", Args);
        if (eventName.StartsWith("\"") && eventName.EndsWith("\""))
            eventName = eventName[1..^1];
        
        var eventID = EventID.Null;
        switch (eventName.ToLower())
        {
            case "west_path_gate_unlocked":
            case var _ when eventName.ToLower().Contains("west") && eventName.ToLower().Contains("gate") && eventName.ToLower().Contains("unlocked"):
                eventID = EventID.West_Path_Gate_Unlocked;
                break;
            
            case "gemstone_cavern_unlocked":
            case var _ when eventName.ToLower().Contains("gemstone") && eventName.ToLower().Contains("cavern") && eventName.ToLower().Contains("unlocked"):
                eventID = EventID.Gemstone_Cavern_Unlocked;
                break;

            case "orchard_unlocked":
            case var _ when eventName.ToLower().Contains("orchard") && eventName.ToLower().Contains("unlocked"):
                eventID = EventID.Orchard_Unlocked;
                break;

            case "satellite_raised":
            case var _ when eventName.ToLower().Contains("satellite") && eventName.ToLower().Contains("raised"):
                eventID = EventID.Satellite_Raised;
                break;
            
            case "blackbridge_powered":
            case var _ when eventName.ToLower().Contains("blackbridge") && eventName.ToLower().Contains("powered"):
                eventID = EventID.Blackbridge_Powered;
                break;

            default:
                ArchipelagoConsole.LogMessage($"Unknown event name: {eventName}");
                return;
        }

        ModInstance.StatsLogger.GetComponent<StatsLogger>().Record_Event(eventID);
    }
}
