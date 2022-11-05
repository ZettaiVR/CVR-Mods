using ABI_RC.Core.EventSystem;
using ABI_RC.Core.Networking.API.Responses;
using System.Runtime.CompilerServices;

namespace Zettai
{
    public class Tags
    {
        private uint Data;
        public bool LoudAudio { get => GetBool(TagsEnum.LoudAudio); private set => SetBool(TagsEnum.LoudAudio, value); }
        public bool LongRangeAudio { get => GetBool(TagsEnum.LongRangeAudio); private set => SetBool(TagsEnum.LongRangeAudio, value); }
        public bool ContainsMusic { get => GetBool(TagsEnum.ContainsMusic); private set => SetBool(TagsEnum.ContainsMusic, value); }
        public bool ScreenEffects { get => GetBool(TagsEnum.ScreenEffects); private set => SetBool(TagsEnum.ScreenEffects, value); }
        public bool FlashingColors { get => GetBool(TagsEnum.FlashingColors); private set => SetBool(TagsEnum.FlashingColors, value); }
        public bool FlashingLights { get => GetBool(TagsEnum.FlashingLights); private set => SetBool(TagsEnum.FlashingLights, value); }
        public bool ExtremelyBright { get => GetBool(TagsEnum.ExtremelyBright); private set => SetBool(TagsEnum.ExtremelyBright, value); }
        public bool ParticleSystems { get => GetBool(TagsEnum.ParticleSystems); private set => SetBool(TagsEnum.ParticleSystems, value); }
        public bool Violence { get => GetBool(TagsEnum.Violence); private set => SetBool(TagsEnum.Violence, value); }
        public bool Gore { get => GetBool(TagsEnum.Gore); private set => SetBool(TagsEnum.Gore, value); }
        public bool Horror { get => GetBool(TagsEnum.Horror); private set => SetBool(TagsEnum.Horror, value); }
        public bool Jumpscare { get => GetBool(TagsEnum.Jumpscare); private set => SetBool(TagsEnum.Jumpscare, value); }
        public bool ExtremelyHuge { get => GetBool(TagsEnum.ExtremelyHuge); private set => SetBool(TagsEnum.ExtremelyHuge, value); }
        public bool ExtremelySmall { get => GetBool(TagsEnum.ExtremelySmall); private set => SetBool(TagsEnum.ExtremelySmall, value); }
        public bool Suggestive { get => GetBool(TagsEnum.Suggestive); private set => SetBool(TagsEnum.Suggestive, value); }
        public bool Nudity { get => GetBool(TagsEnum.Nudity); private set => SetBool(TagsEnum.Nudity, value); }
        public bool AdminBanned { get => GetBool(TagsEnum.AdminBanned); private set => SetBool(TagsEnum.AdminBanned, value); }
        public bool Incompatible { get => GetBool(TagsEnum.Incompatible); private set => SetBool(TagsEnum.Incompatible, value); }
        public bool LargeFileSize { get => GetBool(TagsEnum.LargeFileSize); private set => SetBool(TagsEnum.LargeFileSize, value); }
        public bool ExtremeFileSize { get => GetBool(TagsEnum.ExtremeFileSize); private set => SetBool(TagsEnum.ExtremeFileSize, value); }

        public UgcTagsData UgcTagsData => new UgcTagsData
        {
            LoudAudio =                                  LoudAudio,
            LongRangeAudio =                                  LongRangeAudio,
            ContainsMusic =                                  ContainsMusic,
            ScreenEffects =                                  ScreenEffects,
            FlashingColors =                                  FlashingColors,
            FlashingLights =                                  FlashingLights,
            ExtremelyBright =                                  ExtremelyBright,
            ParticleSystems =                                  ParticleSystems,
            Violence =                                  Violence,
            Gore =                                  Gore,
            Horror =                                  Horror,
            Jumpscare =                                  Jumpscare,
            ExtremelyHuge =                                  ExtremelyHuge,
            ExtremelySmall =                                  ExtremelySmall,
            Suggestive =                                  Suggestive,
            Nudity =                                  Nudity,
            AdminBanned =                                  AdminBanned,
            Incompatible =                                  Incompatible,
            LargeFileSize =                                  LargeFileSize,
            ExtremeFileSize =                                  ExtremeFileSize
        };
        public AssetManagement.PropTags PropTags => new AssetManagement.PropTags
        {
            LoudAudio =                                  LoudAudio,
            LongRangeAudio =                                  LongRangeAudio,
            ContainsMusic =                                  ContainsMusic,
            ScreenFx =                                  ScreenEffects,
            FlashingColors =                                  FlashingColors,
            FlashingLights =                                  FlashingLights,
            ExtremelyBright =                                  ExtremelyBright,
            ParticleSystems =                                  ParticleSystems,
            Violence =                                  Violence,
            Gore =                                  Gore,
            Horror =                                  Horror,
            Jumpscare =                                  Jumpscare,
            ExcessivelyHuge =                                  ExtremelyHuge,
            ExcessivelySmall =                                  ExtremelySmall,
            Suggestive =                                  Suggestive,
            Nudity =                                  Nudity,
            AdminBanned =                                  AdminBanned,
            Incompatible =                                  Incompatible,
            LargeFileSize =                                  LargeFileSize,
            ExtremeFileSize =                                  ExtremeFileSize,
        };
        public AssetManagement.AvatarTags AvatarTags => new AssetManagement.AvatarTags
        {
            LoudAudio =                                  LoudAudio,
            LongRangeAudio =                                  LongRangeAudio,
            ContainsMusic =                                  ContainsMusic,
            ScreenFx =                                  ScreenEffects,
            FlashingColors =                                  FlashingColors,
            FlashingLights =                                  FlashingLights,
            ExtremelyBright =                                  ExtremelyBright,
            ParticleSystems =                                  ParticleSystems,
            Violence =                                  Violence,
            Gore =                                  Gore,
            Horror =                                  Horror,
            Jumpscare =                                  Jumpscare,
            ExcessivelyHuge =                                  ExtremelyHuge,
            ExcessivelySmall =                                  ExtremelySmall,
            Suggestive =                                  Suggestive,
            Nudity =                                  Nudity,
            AdminBanned =                                  AdminBanned,
            Incompatible =                                  Incompatible,
            LargeFileSize =                                  LargeFileSize,
            ExtremeFileSize =                                  ExtremeFileSize,
        };
        public Tags() => Data = 0;
        public Tags(AssetManagement.AvatarTags tags)
        {
            Data =                                  0;
            LoudAudio =                                  tags.LoudAudio;
            LongRangeAudio =                                  tags.LongRangeAudio;
            ContainsMusic =                                  tags.ContainsMusic;
            ScreenEffects =                                  tags.ScreenFx;
            FlashingColors =                                  tags.FlashingColors;
            FlashingLights =                                  tags.FlashingLights;
            ExtremelyBright =                                  tags.ExtremelyBright;
            ParticleSystems =                                  tags.ParticleSystems;
            Violence =                                  tags.Violence;
            Gore =                                  tags.Gore;
            Horror =                                  tags.Horror;
            Jumpscare =                                  tags.Jumpscare;
            ExtremelyHuge =                                  tags.ExcessivelyHuge;
            ExtremelySmall =                                  tags.ExcessivelySmall;
            Suggestive =                                  tags.Suggestive;
            Nudity =                                  tags.Nudity;
            AdminBanned =                                  tags.AdminBanned;
            Incompatible =                                  tags.Incompatible;
            LargeFileSize =                                  tags.LargeFileSize;
            ExtremeFileSize =                                  tags.ExtremeFileSize;
        }
        public Tags(AssetManagement.PropTags tags)
        {
            Data =                                  0;
            LoudAudio =                                  tags.LoudAudio;
            LongRangeAudio =                                  tags.LongRangeAudio;
            ContainsMusic =                                  tags.ContainsMusic;
            ScreenEffects =                                  tags.ScreenFx;
            FlashingColors =                                  tags.FlashingColors;
            FlashingLights =                                  tags.FlashingLights;
            ExtremelyBright =                                  tags.ExtremelyBright;
            ParticleSystems =                                  tags.ParticleSystems;
            Violence =                                  tags.Violence;
            Gore =                                  tags.Gore;
            Horror =                                  tags.Horror;
            Jumpscare =                                  tags.Jumpscare;
            ExtremelyHuge =                                  tags.ExcessivelyHuge;
            ExtremelySmall =                                  tags.ExcessivelySmall;
            Suggestive =                                  tags.Suggestive;
            Nudity =                                  tags.Nudity;
            AdminBanned =                                  tags.AdminBanned;
            Incompatible =                                  tags.Incompatible;
            LargeFileSize =                                  tags.LargeFileSize;
            ExtremeFileSize =                                  tags.ExtremeFileSize;
        }
        public Tags(UgcTagsData tags)
        {
            Data =                                  0;
            LoudAudio =                                  tags.LoudAudio;
            LongRangeAudio =                                  tags.LongRangeAudio;
            ContainsMusic =                                  tags.ContainsMusic;
            ScreenEffects =                                  tags.ScreenEffects;
            FlashingColors =                                  tags.FlashingColors;
            FlashingLights =                                  tags.FlashingLights;
            ExtremelyBright =                                  tags.ExtremelyBright;
            ParticleSystems =                                  tags.ParticleSystems;
            Violence =                                  tags.Violence;
            Gore =                                  tags.Gore;
            Horror =                                  tags.Horror;
            Jumpscare =                                  tags.Jumpscare;
            ExtremelyHuge =                                  tags.ExtremelyHuge;
            ExtremelySmall =                                  tags.ExtremelySmall;
            Suggestive =                                  tags.Suggestive;
            Nudity =                                  tags.Nudity;
            AdminBanned =                                  tags.AdminBanned;
            Incompatible =                                  tags.Incompatible;
            LargeFileSize =                                  tags.LargeFileSize;
            ExtremeFileSize =                                  tags.ExtremeFileSize;
        }
        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            if (LoudAudio) sb.Append("LoudAudio|");
            if (LongRangeAudio) sb.Append("LongRangeAudio|");
            if (ContainsMusic) sb.Append("ContainsMusic|");
            if (ScreenEffects) sb.Append("ScreenEffects|");
            if (FlashingColors) sb.Append("FlashingColors|");
            if (FlashingLights) sb.Append("FlashingLights|");
            if (ExtremelyBright) sb.Append("ExtremelyBright|");
            if (ParticleSystems) sb.Append("ParticleSystems|");
            if (Violence) sb.Append("Violence|");
            if (Gore) sb.Append("Gore|");
            if (Horror) sb.Append("Horror|");
            if (Jumpscare) sb.Append("Jumpscare|");
            if (ExtremelyHuge) sb.Append("ExtremelyHuge|");
            if (ExtremelySmall) sb.Append("ExtremelySmall|");
            if (Suggestive) sb.Append("Suggestive|");
            if (Nudity) sb.Append("Nudity|");
            if (AdminBanned) sb.Append("AdminBanned|");
            if (Incompatible) sb.Append("Incompatible|");
            if (LargeFileSize) sb.Append("LargeFileSize|");
            if (ExtremeFileSize) sb.Append("ExtremeFileSize|");
            if (sb.Length > 0)
                sb.Remove(sb.Length - 1, 1);
            return sb.ToString();
        }
        private enum TagsEnum
        {
            None,
            LoudAudio,
            LongRangeAudio,
            ContainsMusic,
            ScreenEffects,
            FlashingColors,
            FlashingLights,
            ExtremelyBright,
            ParticleSystems,
            Violence,
            Gore,
            Horror,
            Jumpscare,
            ExtremelyHuge,
            ExtremelySmall,
            Suggestive,
            Nudity,
            AdminBanned,
            Incompatible,
            LargeFileSize,
            ExtremeFileSize,
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool GetBool(TagsEnum index) => (Data >> (int)index & 0x1) == 1;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetBool(TagsEnum index, bool boolToSet)
        {
            uint shifted = ((uint)1) << (int)index;
            Data = Data & ~shifted | (boolToSet ? shifted : 0);
        }
    }
}
