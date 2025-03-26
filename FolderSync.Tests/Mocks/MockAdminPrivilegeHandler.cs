using PeriodicFolderSync.Interfaces;

namespace FolderSync.Tests.Mocks
{
    public class MockAdminPrivilegeHandler : IAdminPrivilegeHandler
    {
        public bool IsRunningAsAdmin()
        {
            return true;
        }

        public void RestartAsAdmin(string?[] args)
        {
        }

        bool IAdminPrivilegeHandler.RestartAsAdmin(string?[] args)
        {
            throw new NotImplementedException();
        }
    }
}