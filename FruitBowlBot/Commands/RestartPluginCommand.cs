﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace JefBot.Commands
{
	internal class RestartPluginCommand : IPluginCommand
    {
        public string PluginName => "Reset";
        public string Command => "reset";
		public IEnumerable<string> Help => new[] { "!reset to restart the bot" };
        public IEnumerable<string> Aliases => new string[0];
        public bool Loaded { get; set; } = true;


        public async Task<string> Action(Message message)
        {
            string res = null;
            await Task.Run(() => { res = Kill(message); }).ConfigureAwait(false);
            return res;
        }

        public string Kill(Message message)
        {
            if (message.IsModerator)
            {
                Restart();
                return "You'll never get this";
            }
            return "Fuck you, it's January! " + message.Username;
        }

        public static void Restart()
        {
            Process.Start(Application.StartupPath + "\\FruitBowlBot.exe");
            Process.GetCurrentProcess().Kill();
        }
    }
}
