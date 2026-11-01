namespace MyHospitalAI.Features.Common;

// Reads the secrets (admin login, token key, AI provider keys) from a .env file into the environment variables,
// so the same file works for `dotnet run`, Docker (env_file: .env) and a server. It looks in the current folder
// and then each parent folder, and stops at the first .env it finds. A variable that is already set is not
// changed, so real environment variables (e.g. set by the hosting service) win. It must run before the host is
// built, because that is when environment variables are read into the configuration.
public static class DotEnv
{
    public static void Load()
    {
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
