using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegalDesktop.Models.Dtos
{
    public class DocumentToSignDto
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public byte[] Content { get; set; }
        public string Type { get; set; }
        public DateTime CreatedAt { get; set; }

        public int SecretaryId { get; set; }

        public string PrivateMessage { get; set; }

        public BackgroundDcToSignDto BackgroundDcToSignDto { get; set; }
    }
}
