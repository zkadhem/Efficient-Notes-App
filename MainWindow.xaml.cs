using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
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
                using (var reader = new StreamReader(request.InputStream, Encoding.UTF8))
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
            var paragraph = new System.Windows.Documents.Paragraph(new Run(text)) { LineHeight = 16 };
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
                var paragraph = new System.Windows.Documents.Paragraph(new Run(content)) { LineHeight = 16 };
                NotesEditor.Document.Blocks.Add(paragraph);
            }
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt|PDF Files (*.pdf)|*.pdf|Word Files (*.docx)|*.docx"
            };

            if (dialog.ShowDialog() == true)
            {
                string filePath = dialog.FileName;
                string fileExtension = System.IO.Path.GetExtension(filePath).ToLower();

                switch (fileExtension)
                {
                    case ".txt":
                        NotesEditor.Document.Blocks.Clear();
                        NotesEditor.Document.Blocks.Add(new System.Windows.Documents.Paragraph(new Run(File.ReadAllText(filePath))));
                        break;
                    case ".pdf":
                        NotesEditor.Document.Blocks.Clear();
                        NotesEditor.Document.Blocks.Add(new System.Windows.Documents.Paragraph(new Run(ExtractTextFromPdf(filePath))));
                        break;
                    case ".docx":
                        NotesEditor.Document.Blocks.Clear();
                        NotesEditor.Document.Blocks.Add(new System.Windows.Documents.Paragraph(new Run(ExtractTextFromDocx(filePath))));
                        break;
                    default:
                        MessageBox.Show("Unsupported file format.");
                        break;
                }
            }
        }

        private string ExtractTextFromPdf(string filePath)
        {
            var text = new StringBuilder();
            using (var pdfReader = new PdfReader(filePath))
            {
                for (int i = 1; i <= pdfReader.NumberOfPages; i++)
                {
                    text.Append(PdfTextExtractor.GetTextFromPage(pdfReader, i));
                }
            }
            return text.ToString();
        }

        private string ExtractTextFromDocx(string filePath)
        {
            var text = new StringBuilder();
            using (var document = DocX.Load(filePath))
            {
                foreach (var paragraph in document.Paragraphs)
                {
                    text.AppendLine(paragraph.Text);
                }
            }
            return text.ToString();
        }

        private void FontSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FontSizeComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                double fontSize = double.Parse((string)selectedItem.Content);
                NotesEditor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, fontSize);
            }
        }

        private void ColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ColorComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string colorName = selectedItem.Content.ToString();
                var color = (Color)ColorConverter.ConvertFromString(colorName);
                NotesEditor.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(color));
            }
        }

        private void AlignLeft_Click(object sender, RoutedEventArgs e)
        {
            NotesEditor.Selection.ApplyPropertyValue(Paragraph.TextAlignmentProperty, TextAlignment.Left);
        }

        private void AlignCenter_Click(object sender, RoutedEventArgs e)
        {
            NotesEditor.Selection.ApplyPropertyValue(Paragraph.TextAlignmentProperty, TextAlignment.Center);
        }

        private void AlignRight_Click(object sender, RoutedEventArgs e)
        {
            NotesEditor.Selection.ApplyPropertyValue(Paragraph.TextAlignmentProperty, TextAlignment.Right);
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
                    ExportTextToPdf(path, noteContent);
                }
                else if (path.EndsWith(".docx"))
                {
                    ExportTextToDocx(path, noteContent);
                }
            }
        }

        private void ExportTextToPdf(string filePath, string content)
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

        private void ExportTextToDocx(string filePath, string content)
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