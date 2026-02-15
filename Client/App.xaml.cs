using System.Windows;

namespace HardcoreClient
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Показываем окно входа/регистрации
            var loginWindow = new LoginWindow();
            bool? result = loginWindow.ShowDialog();

            if (result == true)
            {
                // Пользователь успешно вошёл - открываем главное окно
                var mainWindow = new MainWindow(
                    loginWindow.AuthenticatedUsername,
                    loginWindow.AuthenticatedSocket
                );
                mainWindow.Show();
            }
            else
            {
                // Пользователь закрыл окно входа - выходим из приложения
                Shutdown();
            }
        }
    }
}
