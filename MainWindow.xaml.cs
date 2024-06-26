﻿using Microsoft.VisualBasic;
using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Converters;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AdvancedAttributesChanger
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            SingleAddCombo.Command = AddItem;
        }

        private void OpenFileDialog(object sender, RoutedEventArgs e) {
            // Configure open file dialog box
            Microsoft.Win32.OpenFileDialog dialog = new();

            // Show open file dialog box
            bool? result = dialog.ShowDialog();

            // Process open file dialog box results
            if (result == true)
            {
                FilePathSelection.Text = dialog.FileName;
                RunFile.IsChecked = true;
            }

            e.Handled = true;
        }

        private void OpenFolderDialog(object sender, RoutedEventArgs e) {
            // Configure open folder dialog box
            Microsoft.Win32.OpenFolderDialog dialog = new();
            dialog.Multiselect = false;
            dialog.Title = "Select a folder";

            // Show open folder dialog box
            bool? result = dialog.ShowDialog();

            // Process open folder dialog box results
            if (result == true) {
                // Get the selected folder
                DirectoryPathSelection.Text = dialog.FolderName;
                RunDirectory.IsChecked = true;
            }

            e.Handled = true;
        }
        private async void Path_Changed(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;

            ProgressDialog progressDialog = new ProgressDialog();
            progressDialog.Show();
            progressDialog.UpdateMessage("Preview List", "Getting elements from file system...");

            await RenderAttributeViewerList(sender, e, progressDialog);

            progressDialog.Close();
            Mouse.OverrideCursor = Cursors.Arrow;
        }
        private async Task RenderAttributeViewerList(object sender, RoutedEventArgs e, ProgressDialog progressDialog) {
            TextBox textBox = new TextBox();
            string textBoxPath = "";
            bool? isRecursive = false;
            bool? includeDir = false;
            if (sender is TextBox)
            {
                textBox = (TextBox)sender;
                if (textBox == null) { return; }
                textBoxPath = textBox.Text.ToString();
            }
            if (sender is CheckBox)
            {
                Trace.WriteLine("checkbox");
                CheckBox checkBox = (CheckBox)sender;
                if (checkBox == null) { return; }
                
                if (checkBox.Name.Equals("RunRecursive"))
                    isRecursive = checkBox.IsChecked;
                if (checkBox.Name.Equals("includeDir"))
                    includeDir = checkBox.IsChecked;

                textBox = DirectoryPathSelection;
                textBoxPath = DirectoryPathSelection.Text.ToString();
            }

            Trace.WriteLine(textBox.Name);

            if (textBox.Name.Equals("FilePathSelection") && textBoxPath != "") {
                if (File.Exists(textBoxPath) == false) { return; }
                RunFile.IsChecked = true;
                
                AttributesPreviewList.Items.Clear();

                StackPanel itemToAdd = CreateViewerItem(textBoxPath, GetAttributeNames(textBoxPath));
                AttributesPreviewList.Items.Add(itemToAdd);
                DirectoryPathSelection.Text = "";
            }

            if (textBox.Name.Equals("DirectoryPathSelection") && textBoxPath != "") {
                if (Directory.Exists(textBoxPath) == false) { return; }
          
                RunDirectory.IsChecked = true;
                AttributesPreviewList.Items.Clear();

                // List<string> filesToProcess = await GetChildren(textBoxPath, RunRecursive.IsChecked, IncludeDir.IsChecked);

                progressDialog.UpdateMessage("Preview List", "Generating preview list...");
                await foreach (String directoryChild in GetChildren(textBoxPath, RunRecursive.IsChecked, IncludeDir.IsChecked))
                {
                    StackPanel itemToAdd = CreateViewerItem(directoryChild, GetAttributeNames(directoryChild));
                    AttributesPreviewList.Items.Add(itemToAdd);
                }

                FilePathSelection.Text = "";
            }
        }

        private async void ApplyChanges_Click(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;

            ProgressDialog progressDialog = new ProgressDialog();
            progressDialog.Show();
            progressDialog.UpdateMessage("File Modification", "Getting elements from file system...");


            await ApplyChanges(sender, e, progressDialog);
            
            progressDialog.Close();
            Mouse.OverrideCursor = Cursors.Arrow;
        }
        private async Task ApplyChanges(object sender, RoutedEventArgs e, ProgressDialog progressDialog) {
            bool? runOnFile = RunFile.IsChecked;
            if (runOnFile == null) { return; }
            bool addSuccess = false;
            bool removeSuccess = false;

            if (runOnFile == true) {
                String path = FilePathSelection.Text;
                if (File.Exists(path) == false) {
                    MessageBox.Show($"File does not exist. ({path})");    
                    return; 
                }
                List<String> attributesToAdd = [];
                List<String> attributesToRemove = [];

                foreach (DockPanel item in SingleAddList.Items) {
                    attributesToAdd.Add(((TextBlock)item.Children[1]).Text.ToString());
                }

                foreach (DockPanel item in SingleRemoveList.Items) {
                    attributesToRemove.Add(((TextBlock)item.Children[1]).Text.ToString());
                }

                progressDialog.UpdateMessage("File Modification", "Applying attributes modification...");

                addSuccess = AddAttributes(path, attributesToAdd);
                removeSuccess = RemoveAttributes(path, attributesToRemove);

                if (FileChangeTime.IsChecked == true)
                {
                    try {
                        File.SetLastWriteTime(path, DateTime.Now);
                    }
                    catch (Exception exception) {
                        MessageBox.Show($"Cannot update modification time for {path}. Stack: {exception}");
                    }
                }

                await RenderAttributeViewerList(FilePathSelection, e, progressDialog);
            } 
            else {
                String path = DirectoryPathSelection.Text;
                if (Directory.Exists(path) == false) {
                    MessageBox.Show($"Folder does not exist. ({path})");    
                    return; 
                }

                List<String> attributesToAdd = [];
                List<String> attributesToRemove = [];

                foreach (DockPanel item in FolderAddList.Items)
                {
                    attributesToAdd.Add(((TextBlock)item.Children[1]).Text.ToString());
                }

                foreach (DockPanel item in FolderRemoveList.Items)
                {
                    attributesToRemove.Add(((TextBlock)item.Children[1]).Text.ToString());
                }

                addSuccess = true;
                removeSuccess = true;

                //List<string> filesToProcess = await GetChildren(path, RunRecursive.IsChecked, IncludeDir.IsChecked);

                progressDialog.UpdateMessage("File Modification", "Applying attributes modification...");
                await foreach (String directoryChild in GetChildren(path, RunRecursive.IsChecked, IncludeDir.IsChecked))
                {
                    bool resultAdd = AddAttributes(directoryChild, attributesToAdd);
                    bool resultRemove = RemoveAttributes(directoryChild, attributesToRemove);

                    if (DirectoryChangeTime.IsChecked == true)
                    {
                        try
                        {
                            File.SetLastWriteTime(directoryChild, DateTime.Now);
                        }
                        catch (UnauthorizedAccessException)
                        {
                            MessageBox.Show($"Cannot update modification time for {path} due to a lack of permissions.");
                        }
                        catch (Exception exception)
                        {
                            MessageBox.Show($"Cannot update modification time for {path}. Stack: {exception}");
                        }
                    }

                    if (resultAdd == false) { addSuccess = false; }
                    if (resultRemove == false) { removeSuccess = false; }
                }

                await RenderAttributeViewerList(DirectoryPathSelection, e, progressDialog);
            }

            if (addSuccess && removeSuccess)
            {
                MessageBox.Show("Changes applied successfully!");
            }
            else {
                MessageBox.Show("Errors happened while trying to apply the changes. Changes applied partially.");
            }

        }

        // Original from https://stackoverflow.com/a/929418/11960264
        private static async IAsyncEnumerable<string> GetChildren(string path, bool? isRecursive, bool? includeDirs)
        {
            Trace.WriteLine($"Path: {path} | Recursive: {isRecursive} | Dirs: {includeDirs}");
            Queue<string> queue = new Queue<string>();
            queue.Enqueue(path);
            while (queue.Count > 0)
            {
                path = queue.Dequeue();
                if (isRecursive == true)
                {
                    try
                    {
                        var subDirs = await Task.Run(() => Directory.EnumerateDirectories(path));
                        foreach (string subDir in subDirs)
                        {
                            queue.Enqueue(subDir);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine(ex);
                    }
                }

                String[]? files = null;
                List<String>? fileList = [];

                try
                {
                    if (includeDirs == true)
                    {
                        fileList.Add(path);
                    }
                    var filesInDir = await Task.Run(() => Directory.EnumerateFiles(path));
                    fileList.AddRange(filesInDir);
                    files = fileList.ToArray<String>();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                }

                if (files != null)
                {
                    for (int i = 0; i < files.Length; i++)
                    {
                        yield return files[i];
                    }
                }
            }
        }

        // GetChildren V2
        //private static async IAsyncEnumerable<string> GetChildren(string path, bool? isRecursive, bool? includeDirs) {
        //    Trace.WriteLine($"Path: {path} | Recursive: {isRecursive} | Dirs: {includeDirs}");
        //    SearchOption searchOption = isRecursive == true ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        //    IEnumerable<string> files;
        //    if (includeDirs == true)
        //    {
        //        files = await Task.Run(() => Directory.EnumerateFileSystemEntries(path, "*.*", searchOption));
        //    } else
        //    {
        //        files = await Task.Run(() => Directory.EnumerateFiles(path, "*.*", searchOption));
        //    }

        //    foreach (string file in files) {
        //        yield return file;
        //    }
        //}

        // GetChildren V3
        //private static async Task<List<string>> GetChildren(string path, bool? isRecursive, bool? includeDirs)
        //{
        //    Trace.WriteLine($"Path: {path} | Recursive: {isRecursive} | Dirs: {includeDirs}");
        //    SearchOption searchOption = isRecursive == true ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        //    List<string> files;
        //    if (includeDirs == true)
        //    {
        //        files = await Task.Run(() => Directory.EnumerateFileSystemEntries(path, "*.*", searchOption).ToList());
        //    }
        //    else
        //    {
        //        files = await Task.Run(() => Directory.EnumerateFiles(path, "*.*", searchOption).ToList());
        //    }

        //    return files;
        //}

        static string GetAttributeNames(String filePath) {
            FileAttributes fileAttribs = File.GetAttributes(filePath);
            Trace.WriteLine(fileAttribs.ToString());
            List<String> attributeNames = [];

            foreach (FileAttributes currentAttrib in Enum.GetValues(fileAttribs.GetType())) {
                if (currentAttrib != FileAttributes.None && fileAttribs.HasFlag(currentAttrib)) {
                    attributeNames.Add(currentAttrib.ToString());
                }
            }

            if (attributeNames.Count == 0) { attributeNames.Add(FileAttributes.None.ToString()); }

            return string.Join(", ", attributeNames);
        }

        static FileAttributes ParseAttributes(List<String> toAddAttributes, bool isDirectory) {
            FileAttributes parsedAttributes = 0;

            foreach (String attrib in toAddAttributes) {
                if (attrib == "Temporary" && isDirectory == true) {
                    Trace.WriteLine("Skipping parse for Temporary when handling a directory.");
                }
                else {
                    if (Enum.TryParse(attrib.Replace(" ", ""), true, out FileAttributes parsedAttrib)) {
                        parsedAttributes |= parsedAttrib;
                    }
                    else {
                        MessageBox.Show($"Could not parse as attribute. ({attrib})");
                        throw new Exception();
                    }
                }
            }

            return parsedAttributes;
        }

        static bool AddAttributes(String filePath, List<String> toAddAttributes) {
            FileAttributes currentAttributes = File.GetAttributes(filePath);
            try {
                FileAttributes parsedAttributes = ParseAttributes(toAddAttributes, currentAttributes.HasFlag(FileAttributes.Directory));
                Trace.WriteLine($"Current attributes: {currentAttributes} | Parsed Attributes: {parsedAttributes}");
                currentAttributes |= parsedAttributes;
                Trace.WriteLine($"Final Attributes: {currentAttributes}");
            }
            catch (Exception) {
                return false;
            }
            Trace.WriteLine(currentAttributes);
            File.SetAttributes(filePath, currentAttributes);
            Trace.WriteLine($"Setted attributes: {File.GetAttributes(filePath)}");
            return true;
        }

        static bool RemoveAttributes(String filePath, List<String> toRemoveAttributes)
        {
            FileAttributes currentAttributes = File.GetAttributes(filePath);
            FileAttributes newAttributes;
            try
            {
                FileAttributes parsedAttributes = ParseAttributes(toRemoveAttributes, currentAttributes.HasFlag(FileAttributes.Directory));
                newAttributes = currentAttributes & ~parsedAttributes;
            }
            catch (Exception)
            {
                return false;
            }

            File.SetAttributes(filePath, newAttributes);
            return true;
        }

        private static StackPanel CreateViewerItem(String filePath, String fileAttributes){
            StackPanel stackPanel = new StackPanel { Orientation = Orientation.Horizontal, MinWidth = 290 };
            TextBox path = new TextBox { Text = filePath, Background = Brushes.Transparent, BorderThickness = new Thickness(0), IsReadOnly = true };
            TextBlock separator = new TextBlock { Text = "=", Margin = new Thickness(5,0,5,0)};
            TextBlock attributes = new TextBlock { Text = fileAttributes };

            stackPanel.Children.Add(path);
            stackPanel.Children.Add(separator);
            stackPanel.Children.Add(attributes);

            return stackPanel;
        }

        private DockPanel CreateItem(String text)
        {
            DockPanel dockPanel = new DockPanel { Width = 155, Margin = new Thickness(5) };

            // button is added first so that it takes the rightmost position.
            Button button = new Button
            {
                Content = "❌",
                VerticalAlignment = VerticalAlignment.Center,
                Width = 20,
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
            };
            button.Click += RemoveItem;
            button.SetResourceReference(Control.TemplateProperty, "ListButtonTemplate");
            DockPanel.SetDock(button, Dock.Right);

            TextBlock textBlock = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(textBlock, Dock.Left);

            dockPanel.Children.Add(button);
            dockPanel.Children.Add(textBlock);

            return dockPanel;
        }

        private void AddItem(object sender, RoutedEventArgs e) {
            ComboBox comboBox = (ComboBox)sender;
            ComboBoxItem selectedItem = (ComboBoxItem)comboBox.SelectedItem;
            if (comboBox == null || selectedItem == null) { return; }

            // Get the parent of comboBox
            DependencyObject parent = VisualTreeHelper.GetParent(comboBox);
            while (parent != null && parent is not AttributesComboBox)
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            if (parent == null) { return; }

            String selectedItemContent = (String)selectedItem.Content;
            AttributesComboBox parentAttributeCombo = (AttributesComboBox)parent;

            DockPanel itemToAdd = CreateItem(selectedItemContent);
            switch (parentAttributeCombo.Name) {
                case "SingleAddCombo":
                    if (IsInList(itemToAdd, SingleAddList)) { break; }
                    SingleAddList.Items.Add(itemToAdd);
                    break;
                case "SingleRemoveCombo":
                    if (IsInList(itemToAdd, SingleRemoveList)) { break; }
                    SingleRemoveList.Items.Add(itemToAdd);
                    break;
                case "FolderAddCombo":
                    if (IsInList(itemToAdd, FolderAddList)) { break; }
                    FolderAddList.Items.Add(itemToAdd);
                    break;
                case "FolderRemoveCombo":
                    if (IsInList(itemToAdd, FolderRemoveList)) { break; }
                    FolderRemoveList.Items.Add(itemToAdd);
                    break;
                default: break;
            }

            comboBox.SelectedIndex = -1;
        }

        private void RemoveItem(object sender, RoutedEventArgs e) {
            Button button = (Button)sender;
            if (button == null) { return; }

            // Get DockPanel ref from Button
            DependencyObject parentDockPanel = VisualTreeHelper.GetParent(button);
            while (parentDockPanel != null && parentDockPanel is not DockPanel) {
                parentDockPanel = VisualTreeHelper.GetParent(parentDockPanel);
            }
            if (parentDockPanel == null) { return; }
            DockPanel itemToDelete = (DockPanel)parentDockPanel;

            // Get ListBox ref from StackPanel
            DependencyObject parentList = VisualTreeHelper.GetParent(itemToDelete);
            while (parentList != null && parentList is not ItemsControl)
            {
                parentList = VisualTreeHelper.GetParent(parentList);
            }
            if (parentList == null) { return; }
            ItemsControl list = (ItemsControl)parentList;

            list.Items.Remove(itemToDelete);

            e.Handled = true;
        }

        private static bool IsInList(DockPanel toFind, ItemsControl list) {
            bool alreadyInList = false;

            foreach (DockPanel item in list.Items)
            {
                TextBlock toFindText = (TextBlock)toFind.Children[1];
                TextBlock currentListItem = (TextBlock)item.Children[1];
                if (toFindText.Text.Equals(currentListItem.Text)) { alreadyInList = true; }
            }

            return alreadyInList;
        }
    }
}