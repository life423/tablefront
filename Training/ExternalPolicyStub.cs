// This project always sets UseExternalAI=true and supplies its own sampled policy actions.
// Keep the linked simulation's Unity inference dependency out of the standalone trainer.
namespace Tablefront
{
    public static class NeuralCommander
    {
        public static bool Available => false;
        public static int TrainingEpisodes => 0;
        public static int Decide(float[] observation, System.Random random) => -1;
    }
}
