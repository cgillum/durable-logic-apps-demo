using System.Collections.Generic;

namespace LogicApps
{
    public class ProjectArtifacts
    {
        public HashSet<string> AppSettings { get; } = new HashSet<string>();

        public Dictionary<string, string> Extensions { get; } = new Dictionary<string, string>();
    }
}
