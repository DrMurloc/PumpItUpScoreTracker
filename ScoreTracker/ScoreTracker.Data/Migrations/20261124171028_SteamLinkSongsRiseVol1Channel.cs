using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <summary>
    ///     Files the nineteen STEAM LINK songs under Phoenix 2's R!SE Vol.1 channel, once, with the
    ///     release that teaches the app the channel (docs/design/song-channels.md §2). They are RISE
    ///     songs on the catalog's own v5 ids: fifteen arcade charts from v1.02.0, four from v1.03.0,
    ///     the arcade's STEAM LINK folder by Andamiro's update notices.
    ///     <para>
    ///         Idempotent, and blind to the songs' Phoenix 2 charts on purpose: it writes a row only
    ///         where the song exists and has no Phoenix 2 channel yet, so a channel set by hand is kept,
    ///         an empty database gets nothing, and a song whose arcade charts arrive after this runs
    ///         already carries its channel when they do. A row on a song with no Phoenix 2 chart is never
    ///         read: the channel reaches every reader through the mix's charts.
    ///     </para>
    /// </summary>
    public partial class SteamLinkSongsRiseVol1Channel : Migration
    {
        private const string Phoenix2 = "A9B7D3C1-52E8-4F06-9B1A-2F8C33E01948";

        private const string SteamLinkSongs = @"
    'C0921627-C0C9-5893-A0C1-C7FD2DFF07DE', -- Transmission
    '62B7F7BA-D3B1-549E-810A-DF4F15A222A2', -- Chess and Chase
    'C394F193-86FE-532C-9020-ACD5806BC8BF', -- Sylaphz
    '7A4C0FE5-AA41-5C00-97B8-0A5D8A5A69E5', -- Pastry mix
    'C2F117E2-1B1D-5588-81AA-820CE0923856', -- Love Witch
    '562FC913-9A4E-571B-B648-69E7B51161FB', -- Galactic Metronome
    '3A54BEF0-1650-5994-ACF7-566BCFD6FBC1', -- Under the Christmas Stars
    '613D7634-A580-5DC3-99B7-F3F3AD2076F6', -- Skybound
    'F2857ACF-096E-5BAC-AA45-BBF9DD128FDD', -- DROP FRONT!ER
    '422CA311-84AA-5088-AA51-B28EC518B574', -- Morrighan
    'A3FD2A37-9A17-5329-B8B4-03D8CC9CF666', -- Galactic Terminus
    '5A09E327-CB94-5916-B061-1A3BACA3D93E', -- The Last Stand
    '20A4B07A-CAF0-5E1F-9AE1-DE47868709D1', -- The Deconstruction
    'C0E6F5ED-9CB6-53A4-A825-E2BFE4C36CF5', -- Reincarnate in ""A""
    '248C3AEB-EE14-575C-BCB4-B4E48222E848', -- Burning SuperNova feat. neur6sia
    'C6DC4D6D-33DE-5AB2-BE20-66BA1B7E0C4D', -- untitled
    'EA7F1647-8F78-5914-91D9-BFF2B583A1EC', -- Time wanderer
    '8454C188-0F44-5755-9E7E-DC483A803A0B', -- Whitemare
    '5F018758-056F-5B61-B82F-66D4A4AEF9D5'  -- Eagle Strike
";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
INSERT INTO [scores].[SongMix] ([SongId], [MixId], [Channel])
SELECT s.[Id], '{Phoenix2}', N'RiseVol1'
FROM [scores].[Song] s
WHERE s.[Id] IN ({SteamLinkSongs})
  AND NOT EXISTS (SELECT 1 FROM [scores].[SongMix] sm WHERE sm.[SongId] = s.[Id] AND sm.[MixId] = '{Phoenix2}');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only the rows this migration could have written; a different channel set by hand stays.
            migrationBuilder.Sql($@"
DELETE FROM [scores].[SongMix]
WHERE [MixId] = '{Phoenix2}' AND [Channel] = N'RiseVol1'
  AND [SongId] IN ({SteamLinkSongs});");
        }
    }
}
