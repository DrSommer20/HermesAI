using HermesAI.MVVM.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace HermesAI.MVVM.Services
{
    public interface IAIConnection
    {
        Task<string> GetResponseAsync(IEnumerable<ChatMessage> chatHistory);


    }
}
