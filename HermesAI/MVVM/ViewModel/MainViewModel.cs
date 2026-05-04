using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HermesAI.MVVM.Model;
using HermesAI.MVVM.Services;
using HermesAI.MVVM.View;
using System.Collections;
using System.Windows;

namespace HermesAI.MVVM.ViewModel
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _inputText = string.Empty;

        [ObservableProperty]
        private Chat _currentChat = new Chat();

        [ObservableProperty]
        private IEnumerable<Chat> _chatList;

        [ObservableProperty]
        private string _mcpStatus = "Keine MCP Server";

        private IChatRepository _chatRepository = new LocalStorageRepository();

        private readonly GeminiConnection _aiConnection = new GeminiConnection();
        private readonly McpClientManager _mcpManager = new McpClientManager();

        /// <summary>
        /// Initializes the MainViewModel, loads chats, and connects to MCP servers.
        /// </summary>
        public MainViewModel()
        {
            ChatList = _chatRepository.GetChats();

            if (ChatList != null && ChatList.Any())
            {
                CurrentChat = ChatList.First();
            }

            // Fire up MCP servers on startup
            _ = InitMcpAsync();
        }

        /// <summary>
        /// Attempts to connect to all enabled MCP servers and updates the UI status.
        /// </summary>
        private async Task InitMcpAsync()
        {
            try
            {
                await _mcpManager.ConnectAllAsync();
                UpdateMcpStatus();
            }
            catch
            {
                McpStatus = "MCP Fehler";
            }
        }

        /// <summary>
        /// Updates the connection status string shown in the UI based on connected MCP servers.
        /// </summary>
        private void UpdateMcpStatus()
        {
            var tools = _mcpManager.GetAllToolInfos();
            McpStatus = _mcpManager.HasConnectedServers
                ? $"🔌 {tools.Count} Tools verfügbar"
                : "Keine MCP Server";
        }

        /// <summary>
        /// Refreshes the chat list from the local repository.
        /// </summary>
        private void RefreshChatList()
        {
            ChatList = _chatRepository.GetChats();
        }

        /// <summary>
        /// Handles sending a user message and orchestrates the Gemini API request,
        /// including the MCP tool loop if tools are available.
        /// </summary>
        [RelayCommand]
        private async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(InputText)) return;

            var userPrompt = InputText;
            CurrentChat.Messages.Add(new ChatMessage(userPrompt, true));
            InputText = string.Empty;

            // Check if any MCP tools are available
            var toolDeclarations = _mcpManager.GetGeminiFunctionDeclarations();

            if (toolDeclarations.Count > 0)
            {
                // ── Tool Mode: Function Calling Loop ──
                await SendWithToolLoopAsync(toolDeclarations);
            }
            else
            {
                // ── Normal Mode: Simple Streaming ──
                await SendSimpleStreamAsync();
            }

            // Auto-rename chat if it's the first message
            if (CurrentChat.Messages.Count(m => !m.IsToolMessage) == 2 && CurrentChat.Title == "Neuer Chat")
            {
                _ = AutoRenameChatAsync(userPrompt);
            }
        }

        /// <summary>
        /// Standard streaming without tools (the original implementation).
        /// </summary>
        private async Task SendSimpleStreamAsync()
        {
            var aiMessage = new ChatMessage("", false);
            CurrentChat.Messages.Add(aiMessage);

            var historyForApi = CurrentChat.Messages
                .Where(m => !m.IsToolMessage)
                .ToList();

            try
            {
                await foreach (var textChunk in _aiConnection.GetResponseStreamAsync(historyForApi))
                {
                    aiMessage.Text += textChunk;
                    await Task.Delay(20);
                }
            }
            catch (Exception ex)
            {
                aiMessage.Text += $"\n\n[Verbindungsabbruch: {ex.Message}]";
            }
        }

        /// <summary>
        /// Sends the message with function calling.
        /// 
        /// WORKFLOW (Tool-Loop):
        /// 1. Send conversation + tools to Gemini
        /// 2. Gemini replies with a functionCall → Execute the tool
        /// 3. Send the result back to Gemini as a functionResponse
        /// 4. Repeat until Gemini replies with text
        /// 5. Stream the final response
        /// 
        /// PROBLEM: Gemini could theoretically chain tool calls forever.
        /// We cap it at 10 iterations just to be safe.
        /// </summary>
        private async Task SendWithToolLoopAsync(List<object> toolDeclarations)
        {
            // Build the conversation history in Gemini's format
            var conversationParts = BuildConversationForGemini();

            const int maxIterations = 10;
            int iteration = 0;

            while (iteration < maxIterations)
            {
                iteration++;

                // Send it off to Gemini (non-streaming so we can catch functionCalls)
                var geminiResponse = await _aiConnection.SendWithToolsAsync(
                    conversationParts, toolDeclarations);

                if (geminiResponse.IsFunctionCall)
                {
                    // ── Tool Call Detected ──

                    // 1. Show the tool call in the UI
                    var argsDisplay = string.Join(", ", geminiResponse.FunctionArgs
                        .Select(kv => $"{kv.Key}: {kv.Value}"));
                    var toolMessage = ChatMessage.CreateToolMessage(
                        geminiResponse.FunctionName,
                        $"🔧 Tool: {geminiResponse.FunctionName}({argsDisplay})");
                    CurrentChat.Messages.Add(toolMessage);

                    // 2. Execute the actual tool via MCP
                    var toolResult = await _mcpManager.CallToolAsync(
                        geminiResponse.FunctionName, geminiResponse.FunctionArgs);

                    // 3. Show the result in the UI
                    var resultMessage = ChatMessage.CreateToolMessage(
                        geminiResponse.FunctionName,
                        $"📋 Ergebnis: {(toolResult.Length > 500 ? toolResult[..500] + "..." : toolResult)}");
                    CurrentChat.Messages.Add(resultMessage);

                    // 4. Append the functionCall and functionResponse to the history
                    conversationParts.Add(new
                    {
                        role = "model",
                        parts = new object[] { new { functionCall = new { name = geminiResponse.FunctionName, args = geminiResponse.FunctionArgs } } }
                    });

                    conversationParts.Add(new
                    {
                        role = "user",
                        parts = new object[] { new { functionResponse = new { name = geminiResponse.FunctionName, response = new { result = toolResult } } } }
                    });

                    // Keep looping - Gemini might need to chain more tools
                    continue;
                }
                else
                {
                    // ── Text Response: Fire up the stream ──
                    var aiMessage = new ChatMessage("", false);
                    CurrentChat.Messages.Add(aiMessage);

                    try
                    {
                        await foreach (var chunk in _aiConnection.StreamWithToolHistoryAsync(
                            conversationParts, toolDeclarations))
                        {
                            aiMessage.Text += chunk;
                            await Task.Delay(20);
                        }
                    }
                    catch (Exception ex)
                    {
                        aiMessage.Text += $"\n\n[Fehler: {ex.Message}]";
                    }

                    break; // Break the loop
                }
            }

            if (iteration >= maxIterations)
            {
                CurrentChat.Messages.Add(new ChatMessage(
                    "[Maximale Tool-Iterationen erreicht. Bitte versuche es erneut.]", false));
            }
        }

        /// <summary>
        /// Builds the conversation history for Gemini (filtering out tool messages).
        /// </summary>
        private List<object> BuildConversationForGemini()
        {
            return CurrentChat.Messages
                .Where(m => !m.IsToolMessage)
                .Select(msg => (object)new
                {
                    role = msg.IsMyMessage ? "user" : "model",
                    parts = new[] { new { text = msg.Text } }
                })
                .ToList();
        }

        /// <summary>
        /// Asks Gemini to generate a short title based on the first prompt of a new chat.
        /// </summary>
        private async Task AutoRenameChatAsync(string prompt)
        {
            var newTitle = await _aiConnection.GenerateTitleAsync(prompt);
            CurrentChat.Title = newTitle;
            _chatRepository.SaveChat(CurrentChat);
            RefreshChatList();
        }

        /// <summary>
        /// Selects a chat from the history list to become the active chat.
        /// </summary>
        [RelayCommand]
        private void SelectChat(Chat selectedChat)
        {
            if (selectedChat != null)
            {
                _chatRepository.SaveChat(CurrentChat);
                CurrentChat = selectedChat;
            }
        }

        /// <summary>
        /// Creates a brand new chat session and saves the previous one.
        /// </summary>
        [RelayCommand]
        private void NewChat()
        {
            _chatRepository.SaveChat(CurrentChat);
            CurrentChat = new Chat { Title = $"Neuer Chat" };
            _chatRepository.SaveChat(CurrentChat);
            RefreshChatList();
        }

        /// <summary>
        /// Safely shuts down the application by saving the current chat and disposing of connections.
        /// </summary>
        [RelayCommand]
        private void CloseWindow(Window window)
        {
            _chatRepository.SaveChat(CurrentChat);
            _ = _mcpManager.DisposeAsync();
            window?.Close();
        }

        /// <summary>
        /// Enables dragging the custom styled window around the screen.
        /// </summary>
        [RelayCommand]
        private void DragWindow(Window window)
        {
            if (Application.Current.MainWindow.WindowState == WindowState.Normal)
            {
                window?.DragMove();
            }
        }

        /// <summary>
        /// Opens the general settings window.
        /// </summary>
        [RelayCommand]
        private void OpenSettings()
        {
            var settingsWin = new SettingsWindow();
            settingsWin.Owner = Application.Current.MainWindow;
            settingsWin.ShowDialog();
        }

        /// <summary>
        /// Opens the MCP Server management window and reconnects servers when it closes.
        /// </summary>
        [RelayCommand]
        private async Task OpenMcpServers()
        {
            var mcpWin = new McpServersWindow(_mcpManager);
            mcpWin.Owner = Application.Current.MainWindow;
            mcpWin.ShowDialog();

            // Reconnect after closing the settings window
            await _mcpManager.ConnectAllAsync();
            UpdateMcpStatus();
        }

        /// <summary>
        /// Opens a dialog to manually rename an existing chat.
        /// </summary>
        [RelayCommand]
        private void RenameChat(Chat chatToRename)
        {
            if (chatToRename == null) return;

            var renameWin = new RenameWindow(chatToRename.Title);
            renameWin.Owner = Application.Current.MainWindow;
            
            if (renameWin.ShowDialog() == true && !string.IsNullOrWhiteSpace(renameWin.NewTitle))
            {
                chatToRename.Title = renameWin.NewTitle;
                _chatRepository.SaveChat(chatToRename);
                RefreshChatList();
            }
        }

        /// <summary>
        /// Deletes a chat from the history and selects another one if the deleted one was active.
        /// </summary>
        [RelayCommand]
        private void DeleteChat(Chat chatToDelete)
        {
            if (chatToDelete == null) return;

            _chatRepository.DeleteChat(chatToDelete);
            
            if (CurrentChat?.Id == chatToDelete.Id)
            {
                RefreshChatList();
                CurrentChat = ChatList?.FirstOrDefault() ?? new Chat { Title = "Neuer Chat" };
                if (ChatList == null || !ChatList.Any())
                {
                    _chatRepository.SaveChat(CurrentChat);
                    RefreshChatList();
                }
            }
            else
            {
                RefreshChatList();
            }
        }
    }
}