﻿using ConsoleDrawing.Structs;
using Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Instrumentation;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleDrawing.Objects
{
    public abstract class Drawable
    {
        internal static readonly List<Drawable> all = new List<Drawable>();
        private static readonly List<string> debug = new List<string>();
        private static readonly object locker = new object();

        private bool m_Initiated = false;
        private bool m_Destroyed = false;
        private bool m_Enabled = true;
        private Drawable m_Parent = null;

        /// <summary>
        /// Gets or sets this objects enabled state.
        /// Disabled object do not receive <seealso cref="Update"/> or <seealso cref="Draw"/> events.
        /// <para>Setting is identical to <see cref="SetEnabled"/></para>
        /// </summary>
        public bool Enabled {
            get => m_Enabled && !m_Destroyed;
            set => SetEnabled(value);
        }

        /// <summary>
        /// Wether or not this object has been destroyed using <see cref="Destroy"/>.
        /// </summary>
        public bool Destroyed => m_Destroyed;

        /// <summary>
        /// Set or get this objects parent.
        /// <para>Setting is identical to <see cref="SetParent"/></para>
        /// </summary>
        public Drawable Parent
        {
            get => m_Parent;
            set => SetParent(value);
        }
        
        private readonly List<Drawable> children = new List<Drawable>();

        /// <summary>
        /// Local position of this object.
        /// </summary>
        public Vector2 LocalPosition { get; set; } = Vector2.Zero;

        /// <summary>
        /// Position relative to this objects parent (if any)
        /// </summary>
        public Vector2 Position {
            get => Parent == null ? LocalPosition : (Parent.Position + LocalPosition);
            set => LocalPosition = Parent == null ? value : (value - Parent.Position);
        }

        /// <summary>
        /// Local z-depth of this object.
        /// </summary>
        public int localZDepth = 0;

        /// <summary>
        /// Z-depth relative to this objects parent (if any)
        /// </summary>
        public int ZDepth {
            get => (Parent?.ZDepth + localZDepth) ?? localZDepth;
            set => localZDepth = (value - Parent?.ZDepth) ?? value;
        }

        /// <summary>
        /// Same as <see cref="Position"/>, but with <seealso cref="int"/> instead of <seealso cref="float"/>.
        /// </summary>
        public Point ApproxPosition => (Point)Position;

        /// <summary>
        /// Same as <see cref="LocalPosition"/>, but with <seealso cref="int"/> instead of <seealso cref="float"/>.
        /// </summary>
        public Point LocalApproxPosition => (Point)LocalPosition;

        public Drawable()
        {
            SetEnabled(true);
        }

        public Drawable(Drawable parent) : this()
        {
            SetParent(parent);
        }

        ~Drawable()
        {
            Destroy();
        }

        /// <summary>
        /// Sets this objects parent.
        /// </summary>
        /// <param name="parent">The new parent. Can be null to set no parent.</param>
        /// <param name="worldPositionStays">If true, the local position is modified to retain the original "world" position.</param>
        public void SetParent(Drawable parent, bool worldPositionStays = true)
        {
            if (m_Parent == parent) return;
            if (m_Destroyed) return;

            Vector2 oldPos = Position;
            int oldZ = ZDepth;
            
            // Remove from old parent
            m_Parent?.children.Remove(this);

            // Add to new parent
            parent?.children.Add(this);

            m_Parent = parent;

            if (worldPositionStays)
            {
                Position = oldPos;
                ZDepth = oldZ;
            }
        }

        /// <summary>
        /// Enables/disables this object.
        /// Disabled object do not receive <seealso cref="Update"/> or <seealso cref="Draw"/> events.
        /// </summary>
        public void SetEnabled(bool state)
        {
            if (m_Destroyed) return;

            if (state)
            {
                //Time.OnEventUpdate += Update;
                //Time.OnEventDraw += Draw;

                if (!m_Initiated)
                {
                    m_Initiated = true;
                    all.Add(this);
                }
            }
            //else
            //{
            //    Time.OnEventUpdate -= Update;
            //    Time.OnEventDraw -= Draw;
            //}
            m_Enabled = state;
        }
        
        protected abstract void Update();
        protected abstract void Draw();

        /// <summary>
        /// Destroy this drawable. Makes it inactive.
        /// </summary>
        public void Destroy()
        {
            if (Destroyed) return;
            
            //Time.OnEventUpdate -= Update;
            //Time.OnEventDraw -= Draw;
            SetEnabled(false);
            m_Destroyed = true;

            // Remove all children
            foreach (var child in children)
            {
                child?.Destroy();
            }

            SetParent(null);

            //lock (all)
            //{
            //    all.Remove(this);
            //}
        }

        /// <summary>
        /// Destroy this drawable after a <paramref name="delay"/>. Makes it inactive.
        /// </summary>
        /// <param name="delay">The delay in seconds.</param>
        public async void Destroy(float delay)
        {
            await Task.Delay((int)(delay * 1000));
            Destroy();
        }

        public static T FindObjectOfType<T> () where T : Drawable
        {
            return all.First(d => d is T) as T;
        }

        public static T[] FindObjectsOfType<T> () where T : Drawable
        {
            return all.Select(d => d is T) as T[];
        }

        public bool IsChildOf(Drawable parent)
        {
            if (Parent == null) return false;
            if (parent == null) return false;

            if (parent == this) return true;
            if (Parent == parent) return true;

            return Parent.IsChildOf(parent);
        }

        public static void print(string format, params object[] args)
        {
#if DEBUG
            debug.Add(string.Format(format, args));
#endif
        }

        public static void print(object arg)
        {
#if DEBUG
            debug.Add(arg?.ToString() ?? "null");
#endif
        }

        internal static void FrameCallback()
        {
            lock (locker)
            {
                // Fix size
                if (Drawing.FixedSize == false)
                {
                    if (Console.WindowWidth != Drawing.BufferWidth || Console.WindowHeight != Drawing.BufferHeight)
                        Drawing.SetWindowSize(Console.WindowWidth, Drawing.BufferHeight);
                }

                lock (all)
                {
                    // Update
                    Input.AnalyzeInput();
                    for (int i = all.Count - 1; i >= 0; i--)
                    {
                        // GC & Update each one
                        Drawable drawable = all[i];
                        if (drawable?.Destroyed ?? true)
                            all.RemoveAt(i);
                        else if (drawable.Enabled)
                            drawable.Update();
                    }

                    // Draw
                    Drawing.ResetColor();
                    Drawing.Clear();

                    all.Sort((a, b) => b.ZDepth.CompareTo(a.ZDepth));
                    foreach (Drawable drawable in all)
                        if (drawable?.Enabled ?? false)
                            drawable.Draw();

#if DEBUG
                    // Debug text
                    Drawing.BackgroundColor = null;
                    Drawing.ForegroundColor = Color.GREEN;
                    int debugLength = debug.Count;
                    for (int i = 0; i < debugLength; i++)
                    {
                        Drawing.SetCursorPosition(0, Drawing.BufferHeight - debugLength + i);
                        Drawing.Write("<DEBUG> " + debug[i]);
                    }
                    debug.Clear();
#endif

                    Drawing.Render();
                }
            }
        }
    }
}
