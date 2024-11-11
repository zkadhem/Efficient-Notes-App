using System;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace FormattedNotesApp
{
    public partial class MainWindow : Window
    {
        private HttpListener _listener;

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
            NotesEditor.Document.Blocks.Add(new Paragraph(new Run(text)));
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

private void NotesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    // Handle selection change here, if needed
    // Example: Clear the NotesEditor when a new item is selected
    NotesEditor.Document.Blocks.Clear();
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