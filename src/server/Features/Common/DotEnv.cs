namespace MyHospitalAI.Features.Common;

// Reads the .env file into the process environment, so `dotnet run` gets the same secrets as `docker run --env-file
// .env`. It looks in the current folder and then in each parent folder, and only runs in Development: Docker and a
// server set their environment variables themselves. A variable that is already set is not changed. It must run
// before the host is built, because that is when the environment variables are read into the configuration.
public static class DotEnv
{
    public static void LoadForDevelopment()
    {
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Development")
        {
            return;
        }

        for (var folder = new DirectoryInfo(Directory.GetCurrentDirectory()); folder is not null; folder = folder.Parent)
        {
            var file = Path.Combine(folder.FullName, ".env");
            if (!File.Exists(file))
            {
                continue;
            }

            foreach (var line in File.ReadAllLines(file))
            {
                var text = line.Trim();
                var equals = text.IndexOf('=');
                if (text.Length == 0 || text.StartsWith('#') || equals <= 0)
                {
                    continue;
                }

                var name = text[..equals].Trim();
                var value = text[(equals + 1)..].Trim().Trim('"');   // a value can contain '=' (base64 keys do)
                if (Environment.GetEnvironmentVariable(name) is null)
                {
                    Environment.SetEnvironmentVariable(name, value);
                }
            }
            return;
        }
    }
}
