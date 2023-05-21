using ABI_RC.Core.Player;

namespace Zettai
{
    struct IkDataPair
    {
        public bool isDone;
        public bool failed;
        public PlayerAvatarMovementData input;
        public DarkRift.DarkRiftReader darkRiftReader;
        public IkDataPair(PlayerAvatarMovementData _input, DarkRift.DarkRiftReader _darkRiftReader)
        {
            isDone = failed = false;
            input = _input;
            darkRiftReader = _darkRiftReader;
        }
    }
}
