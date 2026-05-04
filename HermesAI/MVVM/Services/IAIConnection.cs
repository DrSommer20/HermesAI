using HermesAI.MVVM.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace HermesAI.MVVM.Services
{
    public interface IAIConnection
    {
        IAsyncEnumerable<string> GetResponseStreamAsync(IEnumerable<ChatMessage> chatHistory, CancellationToken cancellationToken = default);
        Task<string> GenerateTitleAsync(string prompt, CancellationToken cancellationToken = default);
    }
}
