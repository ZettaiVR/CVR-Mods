using ABI_RC.Core.Player;

namespace Zettai
{
    struct IkDataPair
    {
        public bool isDone;
        public bool failed;
        public PuppetMaster input;
        public byte[] data;
        public IkDataPair(PuppetMaster _input, byte[] data)
        {
            isDone = failed = false;
            input = _input;
            this.data = data;
        }
    }
}
