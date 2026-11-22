// This isolated rules test deliberately never loads a policy or a Unity Resources asset.
// Learned-policy evaluation belongs to the reinforcement-learning training harness.
namespace Tablefront
{
    public static class NeuralCommander
    {
        public static int Decide(float[] observation, System.Random random) => -1;
    }
}
