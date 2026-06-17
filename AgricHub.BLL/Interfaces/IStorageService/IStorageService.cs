using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace AgricHub.BLL.Interfaces
{
    public interface IStorageService
    {
        /// <summary>Upload a file and return its public URL.</summary>
        Task<string> UploadAsync(Stream stream, string fileName, string folder = "agrichub");

        /// <summary>Delete a file by its public URL or public ID.</summary>
        Task DeleteAsync(string publicUrlOrId);
    }
}
