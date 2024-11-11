using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Xceed.Words.NET;
using Microsoft.Win32;

namespace FormattedNotesApp
{
    public partial class MainWindow : Window
    {
        private HttpListener _listener;
        private Dictionary<string, string> _notes = new Dictionary<string, string>();
        private string _currentNoteTitle;

        public MainWindow()
        {
            InitializeComponent();
            StartHttpServer();
        }

        private void StartHttpServer()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://localhost:8080/");
            _listener.Start();
            _listener.BeginGetContext(OnRequestReceived, _listener);
        }

        private void OnRequestReceived(IAsyncResult result)
        {
            var context = _listener.EndGetContext(result);
            _listener.BeginGetContext(OnRequestReceived, _listener);

            var request = context.Request;
            if (request.HttpMethod == "POST" && request.InputStream != null)
            {
                using (var reader = new System.IO.StreamReader(request.InputStream, Encoding.UTF8))
                {
                    string receivedText = reader.ReadToEnd();
                    Dispatcher.Invoke(() => AppendToNotes(receivedText));
                }
            }

            var response = context.Response;
            response.StatusCode = 200;
            response.Close();
        }

        private void AppendToNotes(string text)
        {
            var paragraph = new System.Windows.Documents.Paragraph(new Run(text));
            paragraph.LineHeight = 16; // Adjust line spacing
            NotesEditor.Document.Blocks.Add(paragraph);
        }

        private void NewNote_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentNote();

            string newNoteTitle = $"Note {_notes.Count + 1}";
            _notes[newNoteTitle] = string.Empty;
            NotesList.Items.Add(newNoteTitle);
            NotesList.SelectedItem = newNoteTitle;
            _currentNoteTitle = newNoteTitle;
        }

        private void SaveCurrentNote()
        {
            if (_currentNoteTitle != null)
            {
                TextRange textRange = new TextRange(NotesEditor.Document.ContentStart, NotesEditor.Document.ContentEnd);
                _notes[_currentNoteTitle] = textRange.Text;
            }
        }

        private void NotesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SaveCurrentNote();

            if (NotesList.SelectedItem != null)
            {
                _currentNoteTitle = NotesList.SelectedItem.ToString();
                LoadNoteContent(_currentNoteTitle);
            }
        }

        private void LoadNoteContent(string noteTitle)
        {
            if (_notes.TryGetValue(noteTitle, out string content))
            {
                NotesEditor.Document.Blocks.Clear();
                var paragraph = new System.Windows.Documents.Paragraph(new Run(content));
                paragraph.LineHeight = 16; // Adjust line spacing
                NotesEditor.Document.Blocks.Add(paragraph);
            }
        }

        private void ExportNote_Click(object sender, RoutedEventArgs e)
        {
            if (_currentNoteTitle == null)
            {
                MessageBox.Show("No note selected for export.");
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt|PDF Files (*.pdf)|*.pdf|Word Files (*.docx)|*.docx",
                FileName = _currentNoteTitle
            };

            if (dialog.ShowDialog() == true)
            {
                string path = dialog.FileName;
                string noteContent = _notes[_currentNoteTitle];

                if (path.EndsWith(".txt"))
                {
                    File.WriteAllText(path, noteContent);
                }
                else if (path.EndsWith(".pdf"))
                {
                    ExportToPdf(path, noteContent);
                }
                else if (path.EndsWith(".docx"))
                {
                    ExportToDocx(path, noteContent);
                }
            }
        }

        private void ExportToPdf(string filePath, string content)
        {
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                var document = new iTextSharp.text.Document();
                PdfWriter.GetInstance(document, stream);
                document.Open();
                document.Add(new iTextSharp.text.Paragraph(content));
                document.Close();
            }
        }

        private void ExportToDocx(string filePath, string content)
        {
            using (var document = DocX.Create(filePath))
            {
                document.InsertParagraph(content);
                document.Save();
            }
        }

        private void Bold_Click(object sender, RoutedEventArgs e)
        {
            ToggleFormatting(TextElement.FontWeightProperty, FontWeights.Bold);
        }

        private void Italic_Click(object sender, RoutedEventArgs e)
        {
            ToggleFormatting(TextElement.FontStyleProperty, FontStyles.Italic);
        }

        private void Underline_Click(object sender, RoutedEventArgs e)
        {
            TextRange selection = new TextRange(NotesEditor.Selection.Start, NotesEditor.Selection.End);
            selection.ApplyPropertyValue(Inline.TextDecorationsProperty, TextDecorations.Underline);
        }

        private void Bullet_Click(object sender, RoutedEventArgs e)
        {
            NotesEditor.CaretPosition.InsertTextInRun("• ");
        }

        private void ToggleFormatting(DependencyProperty property, object value)
        {
            TextRange selection = new TextRange(NotesEditor.Selection.Start, NotesEditor.Selection.End);
            object currentValue = selection.GetPropertyValue(property);
            selection.ApplyPropertyValue(property, currentValue.Equals(value) ? DependencyProperty.UnsetValue : value);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _listener?.Stop();
        }
    }
}