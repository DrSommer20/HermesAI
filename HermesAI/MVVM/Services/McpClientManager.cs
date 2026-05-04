using HermesAI.MVVM.Model;
using ModelContextProtocol.Client;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace HermesAI.MVVM.Services
{
    /// <summary>
    /// Manages all MCP connections and exposes tools to the AI chat.
    /// 
    /// ARCHITECTURE DECISION: This class encapsulates all MCP communication.
    /// It's the sole point of contact with MCP servers. The ViewModel only
    /// knows about the tools (name + schema) and their results (strings).
    /// 
    /// DEVELOPMENT STEPS:
    /// 1. Load/save configuration (JSON file in %APPDATA%/HermesAI/)
    /// 2. Establish MCP client connections via McpClient.CreateAsync()
    /// 3. Gather tools from all servers and prep them for Gemini
    /// 4. Execute tool calls and return the results
    /// </summary>
    public class McpClientManager : IAsyncDisposable
    {
        private readonly string _configPath;
        private readonly Dictionary<string, McpClient> _connectedClients = new();
        private readonly Dictionary<string, McpClientTool> _toolRegistry = new();

        /// <summary>
        /// Initializes the manager and sets up the path for the config file.
        /// </summary>
        public McpClientManager()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folderPath = Path.Combine(appDataPath, "HermesAI");
            Directory.CreateDirectory(folderPath);
            _configPath = Path.Combine(folderPath, "mcp_servers.json");
        }

        // Load / Save configurations
        /// <summary>
        /// Reads the server configs from the local JSON file.
        /// </summary>
        public List<McpServerConfig> LoadConfigs()
        {
            if (!File.Exists(_configPath))
                return new List<McpServerConfig>();

            string json = File.ReadAllText(_configPath);
            return JsonSerializer.Deserialize<List<McpServerConfig>>(json) ?? new List<McpServerConfig>();
        }

        /// <summary>
        /// Saves the given list of server configs to the local JSON file.
        /// </summary>
        public void SaveConfigs(List<McpServerConfig> configs)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(configs, options);
            File.WriteAllText(_configPath, json);
        }

        /// <summary>
        /// Connects to all enabled MCP servers.
        /// 
        /// ARCHITECTURE DECISION: We support two transport types:
        /// - stdio: Spins up a local process (e.g., npx, python, dotnet)
        /// - sse: Connects to an HTTP SSE endpoint (using HttpClientTransport)
        /// 
        /// PROBLEM ENCOUNTERED: The official SDK doesn't have an explicit "SseClientTransport".
        /// Instead, it provides HttpClientTransport, which handles both SSE and Streamable HTTP.
        /// 
        /// NOTE: If a server is unreachable, we just log the error and skip it 
        /// so the whole app doesn't crash and other servers can still work.
        /// </summary>
        public async Task ConnectAllAsync()
        {
            await DisconnectAllAsync();

            var configs = LoadConfigs();
            foreach (var config in configs.Where(c => c.IsEnabled))
            {
                try
                {
                    McpClient client;

                    if (config.TransportType == "sse")
                    {
                        // HTTP transport for remote servers (SSE/Streamable HTTP)
                        client = await McpClient.CreateAsync(
                            new HttpClientTransport(new HttpClientTransportOptions
                            {
                                Endpoint = new Uri(config.Url ?? throw new InvalidOperationException("SSE-URL fehlt")),
                                Name = config.Name,
                            }));
                    }
                    else
                    {
                        // Stdio transport for local processes
                        var args = string.IsNullOrWhiteSpace(config.Arguments)
                            ? Array.Empty<string>()
                            : config.Arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                        client = await McpClient.CreateAsync(
                            new StdioClientTransport(new StdioClientTransportOptions
                            {
                                Command = config.Command,
                                Arguments = args,
                                Name = config.Name,
                            }));
                    }

                    _connectedClients[config.Name] = client;

                    // Register tools exposed by this server
                    var tools = await client.ListToolsAsync();
                    foreach (var tool in tools)
                    {
                        _toolRegistry[tool.Name] = tool;
                    }
                }
                catch (Exception ex)
                {
                    // Skip this server on failure instead of crashing the whole app
                    System.Diagnostics.Debug.WriteLine(
                        $"Failed to connect to MCP Server '{config.Name}': {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Disconnects and cleans up all currently connected MCP servers.
        /// </summary>
        public async Task DisconnectAllAsync()
        {
            foreach (var client in _connectedClients.Values)
            {
                await client.DisposeAsync();
            }
            _connectedClients.Clear();
            _toolRegistry.Clear();
        }

        public bool HasConnectedServers => _connectedClients.Count > 0;

        // ──────────────────────────────────────────────
        // Tool Access
        // ──────────────────────────────────────────────

        /// <summary>
        /// Returns all available tools across all connected servers.
        /// </summary>
        public List<McpToolInfo> GetAllToolInfos()
        {
            return _toolRegistry.Select(kvp => new McpToolInfo
            {
                Name = kvp.Value.Name,
                Description = kvp.Value.Description ?? "",
                ServerName = "MCP"
            }).ToList();
        }

        /// <summary>
        /// Converts all MCP tools into the function_declarations format expected by Gemini.
        /// 
        /// ARCHITECTURE DECISION: MCP tools define their parameters using JSON Schema.
        /// Gemini expects an OpenAPI-compatible schema. Since both are based on JSON Schema, 
        /// we can map the MCP schemas over pretty directly.
        /// 
        /// PROBLEM ENCOUNTERED: The JsonSchema property on McpClientTool is a JsonElement, 
        /// not a ready-to-use Dictionary. We have to parse it manually to transform it 
        /// into the format Gemini wants.
        /// </summary>
        public List<object> GetGeminiFunctionDeclarations()
        {
            var declarations = new List<object>();

            foreach (var tool in _toolRegistry.Values)
            {
                var parameters = new Dictionary<string, object>();

                if (tool.JsonSchema.ValueKind != System.Text.Json.JsonValueKind.Undefined
                    && tool.JsonSchema.ValueKind != System.Text.Json.JsonValueKind.Null)
                {
                    var schemaJson = tool.JsonSchema.ToString();
                    if (!string.IsNullOrEmpty(schemaJson))
                    {
                        var schemaNode = JsonNode.Parse(schemaJson);
                        if (schemaNode?["properties"] != null)
                        {
                            parameters["type"] = "OBJECT";
                            parameters["properties"] = JsonSerializer.Deserialize<Dictionary<string, object>>(
                                schemaNode["properties"]!.ToJsonString()) ?? new();

                            if (schemaNode["required"] != null)
                            {
                                parameters["required"] = JsonSerializer.Deserialize<List<string>>(
                                    schemaNode["required"]!.ToJsonString()) ?? new();
                            }
                        }
                    }
                }

                declarations.Add(new
                {
                    name = tool.Name,
                    description = tool.Description ?? "",
                    parameters = parameters.Count > 0
                        ? parameters
                        : new Dictionary<string, object>
                        {
                            ["type"] = "OBJECT",
                            ["properties"] = new Dictionary<string, object>()
                        }
                });
            }

            return declarations;
        }

        /// <summary>
        /// Executes an MCP tool and returns its output as a string.
        /// 
        /// DEVELOPMENT STEP: We look up the McpClientTool from our registry by name. 
        /// InvokeAsync() then calls the actual MCP server. The response (CallToolResult) 
        /// contains content blocks that we just stringify and return.
        /// </summary>
        public async Task<string> CallToolAsync(string toolName, Dictionary<string, object?> arguments)
        {
            if (!_toolRegistry.TryGetValue(toolName, out var tool))
            {
                return $"[Error: Tool '{toolName}' not found]";
            }

            try
            {
                var aiArgs = new AIFunctionArguments(arguments);
                var result = await tool.InvokeAsync(aiArgs);

                // Convert the result into a readable string
                if (result != null)
                {
                    return result.ToString() ?? "[Empty result]";
                }

                return "[No result returned from tool]";
            }
            catch (Exception ex)
            {
                return $"[Tool execution error: {ex.Message}]";
            }
        }

        /// <summary>
        /// Disposes the manager, ensuring all connections are cleanly closed.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            await DisconnectAllAsync();
        }
    }
}
