using System;

namespace AugmentedCatapultas
{
#if WINDOWS || XBOX
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            using (CoreSection game = new CoreSection())
            {
                game.Run();
            }
        }
    }
#endif
}

