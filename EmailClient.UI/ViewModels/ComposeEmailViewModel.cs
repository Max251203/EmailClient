using EmailClient.Core.Models;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace EmailClient.UI.ViewModels
{
    public class ComposeEmailViewModel : INotifyPropertyChanged
    {
        private readonly EmailAccount _account;
        private string _from;
        private string _to = string.Empty;
        private string _subject = string.Empty;
        private string _body = string.Empty;
        private bool _isLoading;
        private ObservableCollection<Attachment> _attachments;

        public ComposeEmailViewModel(EmailAccount account)
        {
            _account = account;
            _from = account.Email;
            _attachments = new ObservableCollection<Attachment>();

            AttachFileCommand = new RelayCommand(_ => AttachFile());
            RemoveAttachmentCommand = new RelayCommand(attachment => RemoveAttachment(attachment as Attachment));
            SendCommand = new RelayCommand(_ => Send(), _ => CanSend());
        }

        public string From
        {
            get => _from;
            set
            {
                if (_from != value)
                {
                    _from = value;
                    OnPropertyChanged();
                }
            }
        }

        public string To
        {
            get => _to;
            set
            {
                if (_to != value)
                {
                    _to = value;
                    OnPropertyChanged();
                    (SendCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public string Subject
        {
            get => _subject;
            set
            {
                if (_subject != value)
                {
                    _subject = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Body
        {
            get => _body;
            set
            {
                if (_body != value)
                {
                    _body = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<Attachment> Attachments
        {
            get => _attachments;
            set
            {
                if (_attachments != value)
                {
                    _attachments = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasAttachments));
                }
            }
        }

        public bool HasAttachments => Attachments.Any();

        public ICommand AttachFileCommand { get; }
        public ICommand RemoveAttachmentCommand { get; }
        public ICommand SendCommand { get; }

        public event EventHandler? EmailSent;

        private void AttachFile()
        {
            var dialog = new OpenFileDialog
            {
                Multiselect = true,
                Title = "Select files to attach"
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (string fileName in dialog.FileNames)
                {
                    try
                    {
                        var fileInfo = new FileInfo(fileName);
                        var content = File.ReadAllBytes(fileName);

                        var attachment = new Attachment
                        {
                            FileName = Path.GetFileName(fileName),
                            Content = content,
                            ContentType = GetMimeType(fileName),
                            Size = fileInfo.Length
                        };

                        Attachments.Add(attachment);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error attaching file {fileName}: {ex.Message}",
                            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                OnPropertyChanged(nameof(HasAttachments));
            }
        }

        private string GetMimeType(string fileName)
        {
            string extension = Path.GetExtension(fileName).ToLower();
            return extension switch
            {
                ".txt" => "text/plain",
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                _ => "application/octet-stream"
            };
        }

        private void RemoveAttachment(Attachment? attachment)
        {
            if (attachment != null)
            {
                Attachments.Remove(attachment);
                OnPropertyChanged(nameof(HasAttachments));
            }
        }

        private bool CanSend()
        {
            return !string.IsNullOrWhiteSpace(To) && !IsLoading;
        }

        private void Send()
        {
            if (!CanSend()) return;

            try
            {
                IsLoading = true;
                EmailSent?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                IsLoading = false;
            }
        }

        public Email GetEmail()
        {
            return new Email
            {
                From = From,
                To = To.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(e => e.Trim())
                    .ToList(),
                Subject = Subject,
                Body = Body,
                Date = DateTime.Now,
                Attachments = Attachments.ToList()
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}