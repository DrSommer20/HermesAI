using HermesAI.MVVM.Model;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace HermesAI.MVVM.Services
{
    interface IChatRepository
    {
        IEnumerable<Chat> GetChats();
        void SaveChat(Chat chat);
        void DeleteChat(Chat chat);
    }
}
