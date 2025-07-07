using ProjectDataCompression.Models;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        // Console.WriteLine(Path.GetRandomFileName());
        // Console.WriteLine(Path.GetFileNameWithoutExtension(Path.GetRandomFileName()));
        
        ApplicationConfiguration.Initialize();
        Application.Run(new DataCompressionForm());
    }
}