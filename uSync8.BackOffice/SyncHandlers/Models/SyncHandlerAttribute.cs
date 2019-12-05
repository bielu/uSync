﻿using System;

namespace uSync8.BackOffice.SyncHandlers
{
    public class SyncHandlerAttribute : Attribute
    {
        public SyncHandlerAttribute(string alias, string name, string folder, int priority)
        {
            Alias = alias;
            Name = name;
            Priority = priority;
            Folder = folder;
        }

        public string Name { get; set; }
        public string Alias { get; set; }
        public int Priority { get; set; }
        public string Folder { get; set; }

        public bool IsTwoPass { get; set; } = false;

        public string Icon { get; set; }

        public string EntityType { get; set; }
    }
}
