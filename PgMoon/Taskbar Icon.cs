﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;

namespace PgMoon
{
    public class TaskbarIcon : IDisposable
    {
        #region Init
        protected TaskbarIcon(NotifyIcon NotifyIcon, Popup Target)
        {
            this.NotifyIcon = NotifyIcon;
            this.Target = Target;

            LastClosedTime = DateTime.MinValue;
            Target.Closed += OnClosed;
        }

        protected static List<TaskbarIcon> ActiveIconList { get; private set; } = new List<TaskbarIcon>();
        private NotifyIcon NotifyIcon;
        private Popup Target;
        #endregion

        #region Client Interface
        public static TaskbarIcon Create(Icon Icon, string ToolTipText, System.Windows.Controls.ContextMenu Menu, Popup Target)
        {
            try
            {
                NotifyIcon NotifyIcon = new NotifyIcon();
                NotifyIcon.Icon = Icon;
                NotifyIcon.Text = ToolTipText;
                NotifyIcon.Click += OnClick;

                TaskbarIcon NewTaskbarIcon = new TaskbarIcon(NotifyIcon, Target);
                NotifyIcon.ContextMenuStrip = NewTaskbarIcon.MenuToMenuStrip(Menu);
                ActiveIconList.Add(NewTaskbarIcon);

                NotifyIcon.Visible = true;

                return NewTaskbarIcon;
            }
            catch
            {
                throw new IconCreationFailedException();
            }
        }

        public bool ToggleChecked(ICommand Command, out bool IsChecked)
        {
            foreach (KeyValuePair<ToolStripMenuItem, ICommand> Entry in CommandTable)
                if (Entry.Value == Command)
                {
                    ToolStripMenuItem MenuItem = Entry.Key;
                    IsChecked = !MenuItem.Checked;
                    MenuItem.Checked = IsChecked;
                    return true;
                }

            IsChecked = false;
            return false;
        }

        public void UpdateToolTipText(string ToolTipText)
        {
            NotifyIcon.Text = ToolTipText;
        }
        #endregion

        #region Events
        private static void OnClick(object sender, EventArgs e)
        {
            System.Windows.Forms.MouseEventArgs AsMouseEventArgs;
            if ((AsMouseEventArgs = e as System.Windows.Forms.MouseEventArgs) != null)
            {
                foreach (TaskbarIcon Item in ActiveIconList)
                    if (Item.NotifyIcon == sender)
                    {
                        Item.OnClick(AsMouseEventArgs.Button);
                        break;
                    }
            }
        }

        private void OnClick(MouseButtons Button)
        {
            switch (Button)
            {
                case MouseButtons.Left:
                    if (!Target.IsOpen)
                    {
                        if ((DateTime.UtcNow - LastClosedTime).TotalSeconds >= 1.0)
                            Target.IsOpen = true;
                        else
                            LastClosedTime = DateTime.MinValue;
                    }
                    break;
            }
        }

        private static void OnMenuClicked(object sender, EventArgs e)
        {
            ToolStripMenuItem MenuItem;
            if ((MenuItem = sender as ToolStripMenuItem) != null)
                OnMenuClicked(MenuItem);
        }

        private void OnClosed(object sender, EventArgs e)
        {
            LastClosedTime = DateTime.UtcNow;
        }

        private DateTime LastClosedTime;
        #endregion

            #region Menu
        private ContextMenuStrip MenuToMenuStrip(System.Windows.Controls.ContextMenu Menu)
        {
            ContextMenuStrip Result = new ContextMenuStrip();

            ConvertToolStripMenuItems(Menu.Items, Result.Items);

            return Result;
        }

        private void ConvertToolStripMenuItems(System.Windows.Controls.ItemCollection SourceItems, ToolStripItemCollection DestinationItems)
        {
            foreach (System.Windows.Controls.Control Item in SourceItems)
            {
                System.Windows.Controls.MenuItem AsMenuItem;
                System.Windows.Controls.Separator AsSeparator;

                if ((AsMenuItem = Item as System.Windows.Controls.MenuItem) != null)
                    if (AsMenuItem.Items.Count > 0)
                        AddSubmenuItem(DestinationItems, AsMenuItem);
                    else
                        AddMenuItem(DestinationItems, AsMenuItem);
                else if ((AsSeparator = Item as System.Windows.Controls.Separator) != null)
                    AddSeparator(DestinationItems);
            }
        }

        private void AddSubmenuItem(ToolStripItemCollection DestinationItems, System.Windows.Controls.MenuItem AsMenuItem)
        {
            string MenuHeader = AsMenuItem.Header as string;
            ToolStripMenuItem NewMenuItem = new ToolStripMenuItem(MenuHeader);

            ConvertToolStripMenuItems(AsMenuItem.Items, NewMenuItem.DropDownItems);

            DestinationItems.Add(NewMenuItem);
        }

        private void AddMenuItem(ToolStripItemCollection DestinationItems, System.Windows.Controls.MenuItem AsMenuItem)
        {
            string MenuHeader = AsMenuItem.Header as string;

            Bitmap MenuBitmap;
            Icon MenuIcon;

            ToolStripMenuItem NewMenuItem;

            if ((MenuBitmap = AsMenuItem.Icon as Bitmap) != null)
                NewMenuItem = new ToolStripMenuItem(MenuHeader, MenuBitmap);

            else if ((MenuIcon = AsMenuItem.Icon as Icon) != null)
                NewMenuItem = new ToolStripMenuItem(MenuHeader, MenuIcon.ToBitmap());

            else
                NewMenuItem = new ToolStripMenuItem(MenuHeader);

            NewMenuItem.Click += OnMenuClicked;
            NewMenuItem.Visible = (AsMenuItem.Visibility == System.Windows.Visibility.Visible);
            NewMenuItem.Checked = AsMenuItem.IsChecked;

            DestinationItems.Add(NewMenuItem);
            MenuTable.Add(NewMenuItem, this);
            CommandTable.Add(NewMenuItem, AsMenuItem.Command);
        }

        private void AddSeparator(ToolStripItemCollection DestinationItems)
        {
            ToolStripSeparator NewSeparator = new ToolStripSeparator();
            DestinationItems.Add(NewSeparator);
        }

        private static void OnMenuClicked(ToolStripMenuItem MenuItem)
        {
            if (MenuTable.ContainsKey(MenuItem) && CommandTable.ContainsKey(MenuItem))
            {
                TaskbarIcon TaskbarIcon = MenuTable[MenuItem];
                RoutedUICommand Command = CommandTable[MenuItem] as RoutedUICommand;
                if (Command != null)
                    Command.Execute(TaskbarIcon, TaskbarIcon.Target);
            }
        }

        private static Dictionary<ToolStripMenuItem, TaskbarIcon> MenuTable = new Dictionary<ToolStripMenuItem, TaskbarIcon>();
        private static Dictionary<ToolStripMenuItem, ICommand> CommandTable = new Dictionary<ToolStripMenuItem, ICommand>();
        #endregion

        #region Implementation of IDisposable
        protected virtual void Dispose(bool isDisposing)
        {
            if (isDisposing)
                DisposeNow();
        }

        private void DisposeNow()
        {
            using (NotifyIcon ToRemove = NotifyIcon)
            {
                ToRemove.Visible = false;
                ToRemove.Click -= OnClick;

                foreach (TaskbarIcon Item in ActiveIconList)
                    if (Item.NotifyIcon == NotifyIcon)
                    {
                        ActiveIconList.Remove(Item);
                        break;
                    }

                NotifyIcon = null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~TaskbarIcon()
        {
            Dispose(false);
        }
        #endregion
    }

    public class IconCreationFailedException : Exception
    {

    }
}
