using Quartz;
using Quartz.Spi;
using DSharpPlus;

namespace BetBuddy
{
    public class LotteryJobFactory : IJobFactory
    {
        private readonly DiscordClient _discord;

        public LotteryJobFactory(DiscordClient discord)
        {
            _discord = discord;
        }

        public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
        {
            return new LotteryJob(_discord);  // create a new instance of LotteryJob
        }

        public void ReturnJob(IJob job)
        {
            // nothing to do here
        }
    }
}
