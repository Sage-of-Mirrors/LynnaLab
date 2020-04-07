using System;
using Bitmap = System.Drawing.Bitmap;
using System.Collections.Generic;
using Gtk;

namespace LynnaLab
{
    [System.ComponentModel.ToolboxItem(true)]
    public class RoomEditor : TileGridViewer
    {
        public Room Room
        {
            get { return room; }
        }

        public bool ViewObjects {
            get {
                return _viewObjects;
            }
            set {
                base.Hoverable = !value;
                _viewObjects = value;
            }
        }

        public bool ViewObjectBoxes {get; set;}

        protected override Bitmap Image {
            get {
                if (room == null)
                    return null;
                return room.GetImage();
            }
        }

        Room room;
        TileGridViewer client;
        ObjectGroupEditor objectEditor;
        int mouseX=-1,mouseY=-1;

        bool _viewObjects;
        bool draggingObject;

        // Object Group that the object under the cursor belongs to
        ObjectGroup hoveringObjectGroup;
        int hoveringObjectIndex;

        Gdk.ModifierType gdkState;

        public RoomEditor() {
            base.TileWidth = 16;
            base.TileHeight = 16;
            base.Halign = Gtk.Align.Start;
            base.Valign = Gtk.Align.Start;

            ViewObjectBoxes = true;

            this.ButtonPressEvent += delegate(object o, ButtonPressEventArgs args)
            {
                if (client == null)
                    return;
                int x,y;
                args.Event.Window.GetPointer(out x, out y, out gdkState);
                UpdateMouse(x,y);
                if (gdkState.HasFlag(Gdk.ModifierType.Button1Mask))
                    OnClicked(x, y);
                if (IsInBounds(x, y)) {
                    Cairo.Point p = GetGridPosition(x, y);
                    if (gdkState.HasFlag(Gdk.ModifierType.Button3Mask))
                        client.SelectedIndex = room.GetTile(p.X, p.Y);
                }
            };
            this.ButtonReleaseEvent += delegate(object o, ButtonReleaseEventArgs args) {
                int x,y;
                args.Event.Window.GetPointer(out x, out y, out gdkState);
                if (!gdkState.HasFlag(Gdk.ModifierType.Button1Mask)) {
                    draggingObject = false;
                }
            };
            this.MotionNotifyEvent += delegate(object o, MotionNotifyEventArgs args) {
                if (client == null)
                    return;
                int x,y;
                args.Event.Window.GetPointer(out x, out y, out gdkState);
                UpdateMouse(x,y);
                if (gdkState.HasFlag(Gdk.ModifierType.Button1Mask))
                    OnDragged(x, y);
            };
        }

        public void SetClient(TileGridViewer client) {
            this.client = client;
        }

        public void SetObjectGroupEditor(ObjectGroupEditor editor) {
            if (objectEditor != editor) {
                objectEditor = editor;
                objectEditor.RoomEditor = this;
            }
        }

        public void SetRoom(Room r) {
            var handler = new Room.RoomModifiedHandler(OnRoomModified);
            if (room != null)
                room.RoomModifiedEvent -= handler;
            r.RoomModifiedEvent += handler;

            room = r;
            Width = room.Width;
            Height = room.Height;
            QueueDraw();
        }

        public void OnRoomModified() {
            QueueDraw();
        }

        // Called when a new set of objects is loaded or objects are
        // modified or whatever
        public void OnObjectsModified() {
            QueueDraw();
        }

        void UpdateMouse(int x, int y) {
            if (mouseX != x || mouseY != y) {
                mouseX = x;
                mouseY = y;

                if (ViewObjects) // Laziness; not checking where the mouse is
                    QueueDraw();
            }
        }

        void OnClicked(int posX, int posY) {
            Cairo.Point p = GetGridPosition(posX, posY);
            if (!ViewObjects) {
                if (!IsInBounds(posX,posY))
                    return;
                room.SetTile(p.X, p.Y, client.SelectedIndex);
            }
            else {
                if (objectEditor != null) {
                    ObjectGroupEditor editor = objectEditor;
                    if (hoveringObjectGroup != null) {
                        editor.SelectObject(hoveringObjectGroup, hoveringObjectIndex);
                        draggingObject = true;
                    }
                }
            }
        }

        void OnDragged(int x, int y) {
            if (!ViewObjects)
                OnClicked(x,y);
            else {
                if (!draggingObject) return;

                ObjectDefinition obj = objectEditor.SelectedObject;
                if (obj != null && obj.HasXY()) {
                    int newX,newY;
                    if (gdkState.HasFlag(Gdk.ModifierType.ControlMask) || obj.HasShortenedXY()) {
                        newX = x-XOffset;
                        newY = y-YOffset;
                    }
                    else {
                        // Move objects in increments of 8 pixels
                        int unit = 8;
                        int unitLog = (int)Math.Log(unit, 2);

                        int dataX = obj.GetX()+unit/2;
                        int dataY = obj.GetY()+unit/2;
                        int alignX = (dataX)%unit;
                        int alignY = (dataY)%unit;
                        newX = (x-XOffset-alignX)>>unitLog;
                        newY = (y-YOffset-alignY)>>unitLog;
                        newX = newX*unit+alignX+unit/2;
                        newY = newY*unit+alignY+unit/2;
                    }

                    if (newX >= 0 && newX < 256 && newY >= 0 && newY < 256) {
                        obj.SetX((byte)newX);
                        obj.SetY((byte)newY);
                    }

                    QueueDraw();
                }
            }
        }

        protected override bool OnButtonPressEvent(Gdk.EventButton ev)
        {
            // Insert button press handling code here.
            return base.OnButtonPressEvent(ev);
        }

        protected override bool OnDrawn(Cairo.Context cr) {
            base.OnDrawn(cr);

            if (ViewObjects && objectEditor != null) {
                // Draw objects

                int cursorX=-1,cursorY=-1;
                int selectedX=-1,selectedY=-1;

                ObjectGroup group = objectEditor.TopObjectGroup;
                DrawObjectGroup(cr, ref cursorX, ref cursorY, ref selectedX, ref selectedY, group, objectEditor);

                // Object hovering over
                if (cursorX != -1) {
                    cr.Rectangle(cursorX+0.5, cursorY+0.5, 15, 15);
                    cr.SetSourceColor(TileGridViewer.HoverColor);
                    cr.LineWidth = 1;
                    cr.Stroke();
                }
                // Object selected
                if (selectedX != -1) {
                    cr.Rectangle(selectedX+0.5, selectedY+0.5, 15, 15);
                    cr.SetSourceColor(TileGridViewer.SelectionColor);
                    cr.LineWidth = 1;
                    cr.Stroke();
                }
            }

            return true;
        }

        void DrawObjectGroup(Cairo.Context cr, ref int cursorX, ref int cursorY, ref int selectedX, ref int selectedY, ObjectGroup topGroup, ObjectGroupEditor editor) {
            if (topGroup == null)
                return;

            hoveringObjectGroup = null;

            foreach (ObjectGroup group in topGroup.GetAllGroups()) {
                for (int i=0; i<group.GetNumObjects(); i++) {
                    ObjectDefinition obj = group.GetObject(i);
                    Cairo.Color color = ObjectGroupEditor.GetObjectColor(obj.GetObjectType());
                    int x,y;
                    int width;
                    if (!obj.HasXY())
                        continue;

                    x = obj.GetX();
                    y = obj.GetY();
                    width = 16;
                    // Objects with specific positions get
                    // transparency
                    color = new Cairo.Color(color.R,color.G,color.B,0.75);

                    if (editor != null && group == editor.SelectedObjectGroup && i == editor.SelectedIndex) {
                        selectedX = x-8 + XOffset;
                        selectedY = y-8 + YOffset;
                    }
                    if (mouseX-XOffset >= x-8 && mouseX-XOffset < x+8 &&
                            mouseY-YOffset >= y-8 && mouseY-YOffset < y+8) {
                        cursorX = x-8 + XOffset;
                        cursorY = y-8 + YOffset;
                        hoveringObjectGroup = group;
                        hoveringObjectIndex = i;
                    }

                    // x and y are the center coordinates for the object

                    if (ViewObjectBoxes) {
                        cr.SetSourceColor(color);
                        cr.Rectangle(x-width/2+XOffset, y-width/2+YOffset, width, width);
                        cr.Fill();
                    }

                    if (obj.GetGameObject() != null) {
                        try {
                            ObjectAnimationFrame o = obj.GetGameObject().DefaultAnimation.GetFrame(0);
                            o.Draw(cr, x+XOffset, y+YOffset);
                        }
                        catch(NoAnimationException) {
                            // No animation defined
                        }
                        catch(InvalidAnimationException) {
                            // Error parsing an animation; draw a blue X to indicate the error
                            double xPos = x-width/2 + XOffset + 0.5;
                            double yPos = y-width/2 + YOffset + 0.5;

                            cr.SetSourceColor(new Cairo.Color(1.0, 0, 0));
                            cr.MoveTo(xPos, yPos);
                            cr.LineTo(xPos+width-1, yPos+width-1);
                            cr.MoveTo(xPos+width-1, yPos);
                            cr.LineTo(xPos, yPos+width-1);
                            cr.Stroke();
                        }
                    }
                }
            }
        }

        protected override void OnSizeAllocated(Gdk.Rectangle allocation)
        {
            base.OnSizeAllocated(allocation);

            // Offset the image so that objects at position (0,0) can be fully drawn
            base.XOffset = 8;
            base.YOffset = 8;
        }

        // Override preferred width/height so that objects can be drawn even outside normal room
        // boundaries.
        protected override void OnGetPreferredHeight(out int minimum_height, out int natural_height) {
            minimum_height = 17*16;
            natural_height = minimum_height;
        }        
        protected override void OnGetPreferredWidth(out int minimum_width, out int natural_width) {
            minimum_width = 17*16;
            natural_width = minimum_width;
        }        
    }
}
