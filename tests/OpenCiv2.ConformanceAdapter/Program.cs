using System.Text;
using OpenCiv2.Conformance;

try
{
    string input = "";
    if (args is not ["--identity"])
    {
        var builder = new StringBuilder(); char[] buffer = new char[4096]; int count;
        while ((count = Console.In.Read(buffer)) != 0)
        {
            if (count > 16 * 1024 * 1024 - builder.Length) throw new ArgumentException("Adapter input exceeds 16 MiB");
            builder.Append(buffer, 0, count);
        }
        input = builder.ToString();
    }
    Console.WriteLine(args is ["--identity"] ? Adapter.Identity(typeof(Program).Assembly) : Adapter.Run(args, input));
    return 0;
}
catch (Exception error) when (error is not OutOfMemoryException)
{
    Console.Error.WriteLine(error.Message); return 2;
}
