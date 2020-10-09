using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Timers;
using LynnaLab;

namespace Plugins
{
    public class AnimationEditor : Plugin
    {
        PluginManager manager;

        Project Project {
            get {
                return manager.Project;
            }
        }

        public override String Name { get { return "Animation Editor"; } }
        public override String Tooltip { get { return "View and edit sprite animations"; } }
        public override bool IsDockable { get { return false; } }
        public override string Category { get { return "Window"; } }

        // Object currently being viewed/edited
        GameObject m_CurrentGameObject;
        ObjectAnimation m_CurrentAnimation;
        int m_CurrentAnimFrame;

        public ObjectAnimationFrame CurrentFrame
        {
            get
            {
                if (m_CurrentGameObject != null && m_CurrentAnimation != null)
                    return m_CurrentAnimation.GetFrame(m_CurrentAnimFrame);
                else
                    return null;
            }
        }

        // List of object types the user can view/edit animations for
        private string[] ObjectTypes = new string[] { "Enemies", "Interactions", "Special Objects" };

        // Combobox UI
        private Gtk.HBox m_ComboBoxContainer;
        private Gtk.ComboBox m_ObjTypeBox;
        private ComboBoxFromConstants m_ObjConstantBox;

        // Animation UI
        private Gtk.HBox m_AnimationDataContainer;
        private Gtk.TreeView m_AnimList;
        private AnimationViewer m_AnimViewer;

        private Timer m_AnimTimer;
        private int m_CurrentFrameNo;

        public override void Init(PluginManager manager) {
            this.manager = manager;
        }
        public override void Exit() {
        }

        public override void Clicked() {
            Gtk.Window w = new Gtk.Window("Animation Editor");
            m_CurrentAnimFrame = 0;

            Gtk.Alignment frame = new Gtk.Alignment(0,0,0,0);
            Gtk.Alignment tmpAlign = new Gtk.Alignment(0.5f,0,0,0);

            Gtk.VBox vboxx = new Gtk.VBox();

            // Set up comboboxes
            m_ComboBoxContainer = new Gtk.HBox();
            m_ComboBoxContainer.Spacing = 3;

            m_ObjTypeBox = new Gtk.ComboBox(ObjectTypes);
            m_ObjConstantBox = new ComboBoxFromConstants();

            m_ComboBoxContainer.Add(new Gtk.Label("Object Type: "));
            m_ComboBoxContainer.Add(m_ObjTypeBox);
            m_ComboBoxContainer.Add(new Gtk.Label("Object: "));
            m_ComboBoxContainer.Add(m_ObjConstantBox);

            vboxx.Add(m_ComboBoxContainer);

            // Set up animation data viewer
            m_AnimationDataContainer = new Gtk.HBox();
            
            m_AnimList = new Gtk.TreeView();
            m_AnimList.RowActivated += OnTreeViewSelectedChanged;
            Gtk.TreeViewColumn testCol = new Gtk.TreeViewColumn();
            testCol.Title = "Animation List";

            Gtk.CellRendererText testRend = new Gtk.CellRendererText();
            testCol.PackStart(testRend, true);
            testCol.AddAttribute(testRend, "text", 0);

            m_AnimList.AppendColumn(testCol);
            m_AnimList.Model = new Gtk.ListStore(typeof(string));

            m_AnimationDataContainer.Add(m_AnimList);

            m_AnimViewer = new AnimationViewer(this);
            m_AnimationDataContainer.Add(m_AnimViewer);

            vboxx.Add(m_AnimationDataContainer);

            m_ObjTypeBox.Changed += OnTypeBoxChanged;
            m_ObjConstantBox.Changed += OnConstantsBoxChanged;

            m_ObjTypeBox.Active = 0;
            m_ObjConstantBox.Active = 0;

            frame.Add(vboxx);
            w.Add(frame);

            m_AnimTimer = new Timer(16);
            m_AnimTimer.AutoReset = true;
            m_AnimTimer.Elapsed += OnAnimTimerElapsed;
            m_AnimTimer.Enabled = true;

            w.ShowAll();
        }

        private void OnTypeBoxChanged(object sender, EventArgs e)
        {
            m_ObjConstantBox.SetConstantsMapping(GetObjectConstants(ObjectTypes[m_ObjTypeBox.Active]));
            m_ObjConstantBox.Active = 0;
        }

        private void OnConstantsBoxChanged(object sender, EventArgs e)
        {
            m_CurrentGameObject = GetGameObject(ObjectTypes[m_ObjTypeBox.Active], m_ObjConstantBox.ActiveId);
            if (m_CurrentGameObject == null)
                return;

            Gtk.ListStore NewList = new Gtk.ListStore(typeof(string));

            foreach (ObjectAnimation ab in m_CurrentGameObject.Animations)
            {
                NewList.AppendValues(ab.AnimationTableName);
            }

            m_CurrentAnimation = m_CurrentGameObject.Animations[0];
            m_CurrentAnimFrame = 0;
            m_CurrentFrameNo = 0;

            m_AnimList.Model = NewList;
        }

        private void OnTreeViewSelectedChanged(object sender, Gtk.RowActivatedArgs args)
        {
            m_CurrentAnimation = m_CurrentGameObject.Animations[args.Path.Indices[0]];
        }

        private ConstantsMapping GetObjectConstants(string ObjectType)
        {
            switch (ObjectType)
            {
                case "Enemies":
                    return Project.EnemyMapping;
                case "Interactions":
                    return Project.InteractionMapping;
                case "Special Objects":
                    return Project.SpecialObjectMapping;
                default:
                    return Project.EnemyMapping;
            }
        }

        private GameObject GetGameObject(string ObjectType, string ObjectId)
        {
            GameObject test = null;
            
            switch (ObjectType)
            {
                case "Enemies":
                    test = Project.GetIndexedDataType<EnemyObject>(Project.EvalToInt(ObjectId) << 8);
                    break;
                case "Interactions":
                    test = Project.GetIndexedDataType<InteractionObject>(Project.EvalToInt(ObjectId) << 8);
                    break;
                //case "SpecialObjects":
                    //return Project.GetIndexedDataType<SpecialObject>(Project.EvalToInt(ObjectId));
                default:
                    return null;
            }

            return test;
        }

        private void OnAnimTimerElapsed(Object source, ElapsedEventArgs e)
        {
            m_CurrentFrameNo++;

            if (m_CurrentFrameNo >= m_CurrentAnimation.GetFrame(m_CurrentAnimFrame).Length)
            {
                m_CurrentFrameNo = 0;
                m_CurrentAnimFrame++;

                if (m_CurrentAnimFrame >= 2)
                    m_CurrentAnimFrame = 0;
            }

            m_AnimViewer.QueueDraw();
        }
    }

    class AnimationViewer : Gtk.DrawingArea {
        AnimationEditor m_AnimEditor;

        public AnimationViewer(AnimationEditor animEditor) : base() {
            m_AnimEditor = animEditor;
        }

        protected override bool OnDrawn(Cairo.Context cr) {
            cr.Save();
            cr.SetSourceRGB(0.5, 0.5, 0.5);

            if (m_AnimEditor.CurrentFrame != null)
                m_AnimEditor.CurrentFrame.Draw(cr, 32, 32);

            cr.Restore();

            return true;
        }
    }
}