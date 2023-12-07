// See https://aka.ms/new-console-template for more information
using PowerOutageNotifier.PowerOutageNotifierService;

Console.WriteLine("Hello, Docker!");

try
{
    MainService mainService = new MainService();
    await mainService.Start();
}
catch (Exception ex)
{
    Console.WriteLine(ex);
}