using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TaMi_Kassenclient
{
    public class KassenclientBaseForm : Form
    {
        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);


        public static readonly Color HeaderAccent1 = Color.FromArgb(33, 150, 243),  //Color.FromArgb(33, 150, 243),
                                     HeaderAccent2 = Color.FromArgb(25, 118, 210); //Color.FromArgb(33, 203, 243);

        public static readonly Color BackcolorNeutral = Color.FromArgb(33, 150, 243);

        public static readonly Font HeaderTextFont = new Font("Segoe UI Variable", 16F, FontStyle.Bold),
                                    HeaderButtonFont = new Font("Segoe UI Symbol", 14F, FontStyle.Bold),

                                    ButtonFont = new Font("Segoe UI Variable", 12F, FontStyle.Regular);

        public static readonly Color FormBordercolor = Color.FromArgb(13, 71, 161);


        Label lblTitle;
        Button btnMinimize;
        Button btnClose;


        public void SetupDefaultForm(string Name, string title, Size size)
        {
            this.Name = Name;

            this.Text = title;
            this.Icon = Program.AppIcon;

            this.FormBorderStyle = FormBorderStyle.None;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ShowInTaskbar = false;
            this.ClientSize = size;
            
            this.BackColor = Color.WhiteSmoke;

            this.AutoScroll = true;
            this.DoubleBuffered = true;
            this.KeyPreview = true;


            //try { form.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, form.Width, form.Height + 12, 12, 12)); } catch { }

            // Zeichne einen Rahmen um die gesamte Form
            this.Paint += (s, e) =>
            {
                using (var pen = new Pen(FormBordercolor))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, this.ClientSize.Width - 1, this.ClientSize.Height - 1);
                }
            };

            // Bei Größe ändern neu zeichnen
            this.Resize += (s, e) => this.Invalidate();

            //form.KeyDown += PersonalForm_KeyDown;
            //form.KeyPress += PersonalForm_KeyPress;

        }

        public void AddHeaderPanel(string title)
        {
            AddHeaderPanel(title, false, false, false);
        }

        public void AddHeaderPanel(string title, bool closeButton, bool minimizeButton, bool canMoveForm)
        {
            Panel headerPanel = new Panel
            {
                /*Dock = DockStyle.Top,*/
                Location = new Point(0, 0),
                Size = new Size(this.ClientSize.Width, 48),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,

                BackColor = Color.Transparent
            };
            headerPanel.SuspendLayout();

            //Paint-Event für Farbverlauf
            headerPanel.Paint += (s, e) =>
            {
                /*
                using (var brush = new LinearGradientBrush(headerPanel.ClientRectangle, HeaderAccent1, HeaderAccent2, 0f))
                {
                    e.Graphics.FillRectangle(brush, headerPanel.ClientRectangle);
                }
                */

                using (var lg = new LinearGradientBrush(headerPanel.ClientRectangle, HeaderAccent1, HeaderAccent2, 0f))
                    e.Graphics.FillRectangle(lg, headerPanel.ClientRectangle);

                using (var pen = new Pen(FormBordercolor))
                    e.Graphics.DrawLine(pen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);

            };

            // Dragging-Events
            Point mouseDownLocation = Point.Empty;
            bool dragging = false;

            MouseEventHandler dragMouseDown = (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    dragging = true;
                    mouseDownLocation = e.Location;
                }
            };
            MouseEventHandler dragMouseMove = (s, e) =>
            {
                if (dragging)
                {
                    // Position relativ zum Form berechnen
                    var ctrl = s as Control;
                    var offset = ctrl.PointToScreen(e.Location);
                    var formOffset = this.PointToScreen(Point.Empty);
                    this.Left += offset.X - formOffset.X - mouseDownLocation.X;
                    this.Top += offset.Y - formOffset.Y - mouseDownLocation.Y;
                }
            };
            MouseEventHandler dragMouseUp = (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    dragging = false;
            };

            if (canMoveForm)
            {
                headerPanel.MouseDown += dragMouseDown;
                headerPanel.MouseMove += dragMouseMove;
                headerPanel.MouseUp += dragMouseUp;
            }

            this.Controls.Add(headerPanel);

            lblTitle = new Label
            {
                Text = title,
                AutoSize = false,
                
                Location = new Point(16, 0),
                Size = new Size(headerPanel.Width - 16, headerPanel.Height),

                ForeColor = Color.White,
                Font = HeaderTextFont,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                UseMnemonic = false,
            };

            if (canMoveForm)
            {
                lblTitle.MouseDown += dragMouseDown;
                lblTitle.MouseMove += dragMouseMove;
                lblTitle.MouseUp += dragMouseUp;
            }

            headerPanel.Controls.Add(lblTitle);


            //btnClose
            if (closeButton)
            {
                btnClose = new Button
                {
                    Text = "\u2715",
                    Font = HeaderButtonFont,
                    ForeColor = Color.White,
                    BackColor = Color.Transparent,

                    Padding = new Padding(0),
                    Margin = new Padding(0),

                    FlatStyle = FlatStyle.Flat,
                    FlatAppearance = {
                    BorderSize = 0,
                    MouseOverBackColor = Color.FromArgb(255, 80, 80),
                    },

                    Anchor = AnchorStyles.Top | AnchorStyles.Right,

                    Location = new Point(this.ClientSize.Width - 32 - 8, 8),
                    Size = new Size(32, 32),
                    

                    TabStop = false,
                };

                //Maximale Breite des Titels anpassen
                lblTitle.Width = btnClose.Left - lblTitle.Left - 8;

                btnClose.Click += (s, e) => this.Close();
                headerPanel.Controls.Add(btnClose);
            }


            //btnMinimize
            if (minimizeButton)
            {
                btnMinimize = new Button
                {
                    Text = "–",
                    Font = HeaderButtonFont /*new Font("Segoe UI", 16F, FontStyle.Bold)*/,
                    ForeColor = Color.White,
                    BackColor = Color.Transparent,
                    FlatStyle = FlatStyle.Flat,
                    FlatAppearance = {
                    BorderSize = 0,
                    MouseOverBackColor = Color.FromArgb(33, 150, 243, 80),
                    },

                    Anchor = AnchorStyles.Top | AnchorStyles.Right,

                    Location = new Point(this.ClientSize.Width - (closeButton ? btnClose.Width + 40 : 32) - 8, 8),
                    Size = new Size(32, 32),
                    
                    TabStop = false,
                };

                //Maximale Breite des Titels anpassen
                lblTitle.Width = btnMinimize.Left - lblTitle.Left - 8;

                btnMinimize.Click += (s, e) => this.WindowState = FormWindowState.Minimized;
                headerPanel.Controls.Add(btnMinimize);
            }

            headerPanel.ResumeLayout();
        }

    }
}
