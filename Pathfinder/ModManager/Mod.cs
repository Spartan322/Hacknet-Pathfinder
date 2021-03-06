﻿using Pathfinder.Util.File;

namespace Pathfinder.ModManager
{
    public interface IMod
    {
        /// <summary>
        /// Retrieves the Mod's identifier.
        /// </summary>
        /// <value>The Mod's identifier</value>
        string Identifier { get; }
        void Load();
        void LoadContent();
        void Unload();
    }

    public abstract class Mod : IMod
    {
        private ModContent modContent;

        /// <summary>
        /// Retrieves the Mod's identifier.
        /// </summary>
        /// <value>The Mod's identifier</value>
        public abstract string Identifier { get; }

        public virtual string TexturePath => "Texture";
        public virtual string SoundPath => "Sound";
        public virtual string MusicPath => "Music";

        public ModContent ModContent
        {
            get
            {
                if (modContent == null)
                    modContent = new ModContent(Identifier, TexturePath, SoundPath, MusicPath);
                return modContent;
            }
        }

        /// <summary>
        /// Called when the mod is being loaded, use to ensure all mod related stuff is ready to be loaded
        /// </summary>
        /// <remarks>DO NOT USE TO LOAD CONTENT</remarks>
        public abstract void Load();

        /// <summary>
        /// Loads the mod's content.
        /// </summary>
        public abstract void LoadContent();

        /// <summary>
        /// Called when the mod is being unloaded
        /// </summary>
        public abstract void Unload();
    }

    public class Placeholder : IMod
    {
        public Placeholder(string id) { Identifier = id; }
        public string Identifier { get; }
        public void Load() {}
        public void LoadContent() {}
        public void Unload() {}
    }
}
