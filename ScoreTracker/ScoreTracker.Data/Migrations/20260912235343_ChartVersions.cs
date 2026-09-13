using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChartVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AddedInVersionId",
                schema: "scores",
                table: "ChartMix",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MixVersion",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ReleaseDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MixVersion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MixVersion_Mix_MixId",
                        column: x => x.MixId,
                        principalSchema: "scores",
                        principalTable: "Mix",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChartMix_AddedInVersionId",
                schema: "scores",
                table: "ChartMix",
                column: "AddedInVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_MixVersion_MixId_Name",
                schema: "scores",
                table: "MixVersion",
                columns: new[] { "MixId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ChartMix_MixVersion_AddedInVersionId",
                schema: "scores",
                table: "ChartMix",
                column: "AddedInVersionId",
                principalSchema: "scores",
                principalTable: "MixVersion",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // The seed: every patch the sources could name (docs/design/chart-versions.md §8).
            // Ids are minted once, deterministically, so every environment agrees on them.
            migrationBuilder.InsertData(
                schema: "scores",
                table: "MixVersion",
                columns: new[] { "Id", "MixId", "Name", "ReleaseDate", "SortOrder" },
                values: new object[,]
                {
                    { new Guid("ADBCFDF9-BB43-5330-BEE7-BF935CD22E61"), new Guid("A9B7D3C1-52E8-4F06-9B1A-2F8C33E01948"), "1.00.0", new DateOnly(2026, 7, 9), 10 },
                    { new Guid("2EC78033-A748-589B-8935-D4BDD703A581"), new Guid("A9B7D3C1-52E8-4F06-9B1A-2F8C33E01948"), "1.01.0", new DateOnly(2026, 9, 3), 20 },
                    { new Guid("726B39DA-2EFE-5ABD-AF34-45A1CED2654E"), new Guid("1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B"), "1.00.0", new DateOnly(2023, 7, 4), 10 },
                    { new Guid("62614C9F-2323-50E9-B192-9147D0E27931"), new Guid("1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B"), "1.01.0", new DateOnly(2023, 7, 27), 20 },
                    { new Guid("8C43C724-BCFB-54F0-AFBD-39063B1ABC75"), new Guid("1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B"), "1.02.0", new DateOnly(2023, 9, 5), 30 },
                    { new Guid("69D668E6-5CA8-5560-910D-1FEF588E3C69"), new Guid("1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B"), "1.03.0", new DateOnly(2023, 10, 31), 40 },
                    { new Guid("1396C246-0FA3-5282-AF27-01AA7DD58ECF"), new Guid("1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B"), "1.04.0", new DateOnly(2023, 11, 21), 50 },
                    { new Guid("B117F8E7-1CE0-5527-8470-0621CD0490AE"), new Guid("1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B"), "1.05.0", new DateOnly(2023, 12, 21), 60 },
                    { new Guid("AC759442-8BE5-5742-A161-70B1454BD7C7"), new Guid("1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B"), "1.06.0", new DateOnly(2024, 1, 30), 70 },
                    { new Guid("80CBFF7D-52E9-5689-B19E-B0303A76CC0C"), new Guid("1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B"), "1.07.0", new DateOnly(2024, 3, 7), 80 },
                    { new Guid("4F4F6593-0F9E-5389-A46F-4876E4C5DD11"), new Guid("1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B"), "1.08.0", new DateOnly(2024, 4, 18), 90 },
                    { new Guid("F1B1A7AD-B04C-52E5-80E9-98823BC5EA43"), new Guid("1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B"), "2.00.0", new DateOnly(2024, 5, 27), 100 },
                    { new Guid("6B598F47-568A-5711-A438-A6459D288282"), new Guid("1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B"), "2.01.0", new DateOnly(2024, 7, 11), 110 },
                    { new Guid("35BD7FAD-ED57-586C-BBDB-026FAA9ACC02"), new Guid("1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B"), "2.02.0", new DateOnly(2024, 8, 22), 120 },
                    { new Guid("70CB7477-FF66-53AE-9915-8075052C739F"), new Guid("1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B"), "2.03.0", new DateOnly(2024, 9, 26), 130 },
                    { new Guid("47788494-9378-531B-BBC1-9517FFEC249D"), new Guid("1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B"), "2.04.0", new DateOnly(2024, 10, 31), 140 },
                    { new Guid("F07E5911-68B9-53E3-970A-EBB96EC6ACE0"), new Guid("1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B"), "2.05.0", new DateOnly(2024, 11, 28), 150 },
                    { new Guid("B1F58113-0E87-5BD9-A26D-76E4CC4D5063"), new Guid("1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B"), "2.06.0", new DateOnly(2024, 12, 26), 160 },
                    { new Guid("0B83F711-54DB-5C77-B7B4-3D4765D5484E"), new Guid("1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B"), "2.07.0", new DateOnly(2025, 2, 13), 170 },
                    { new Guid("EDAB9227-6E5E-5994-8219-F7CC3ED09668"), new Guid("1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B"), "2.08.0", new DateOnly(2025, 4, 3), 180 },
                    { new Guid("1D9905EE-82B9-5E58-AF21-ADE53F8C51AE"), new Guid("1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B"), "2.09.0", new DateOnly(2025, 5, 27), 190 },
                    { new Guid("667F4A5B-C45A-5DC7-A635-9E5CE2343293"), new Guid("1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B"), "2.10.0", new DateOnly(2025, 7, 24), 200 },
                    { new Guid("25CAE253-31C6-5DFA-87F7-78E4963EB7C8"), new Guid("1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B"), "2.11.0", new DateOnly(2025, 9, 30), 210 },
                    { new Guid("4B4FE03F-6920-5104-98A7-1F5CAC4A8BAB"), new Guid("1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B"), "2.12.0", new DateOnly(2025, 12, 23), 220 },
                    { new Guid("260E97AD-2F35-5789-AB45-9C37F42C6A9B"), new Guid("20F8CCF8-94B1-418D-B923-C375B042BDA8"), "1.00.0", new DateOnly(2019, 1, 7), 10 },
                    { new Guid("D33D74CD-1119-51FA-8787-33A747EB4890"), new Guid("20F8CCF8-94B1-418D-B923-C375B042BDA8"), "1.01.0", new DateOnly(2019, 2, 28), 20 },
                    { new Guid("7EDB6298-20B3-5687-BECF-7AD531E65F7D"), new Guid("20F8CCF8-94B1-418D-B923-C375B042BDA8"), "1.02.0", new DateOnly(2019, 4, 25), 30 },
                    { new Guid("9A3D4061-1988-59AE-8084-1F21E3064D94"), new Guid("20F8CCF8-94B1-418D-B923-C375B042BDA8"), "1.03.0", new DateOnly(2019, 6, 27), 40 },
                    { new Guid("E40C8494-9A98-55C3-B438-D26B44388C2B"), new Guid("20F8CCF8-94B1-418D-B923-C375B042BDA8"), "1.04.0", new DateOnly(2019, 8, 29), 50 },
                    { new Guid("A3240076-6A99-5141-A82A-D8FCD6767A83"), new Guid("20F8CCF8-94B1-418D-B923-C375B042BDA8"), "1.05.0", new DateOnly(2019, 10, 31), 60 },
                    { new Guid("7E282D8D-7287-5833-B23B-D5CB781818E6"), new Guid("20F8CCF8-94B1-418D-B923-C375B042BDA8"), "2.00.0", new DateOnly(2019, 12, 26), 70 },
                    { new Guid("5EA1BD01-0BC9-50B8-BAE6-0405600606E3"), new Guid("20F8CCF8-94B1-418D-B923-C375B042BDA8"), "2.01.0", new DateOnly(2020, 2, 27), 80 },
                    { new Guid("76E059B8-F087-5CA7-B591-AB105AB927AF"), new Guid("20F8CCF8-94B1-418D-B923-C375B042BDA8"), "2.02.0", new DateOnly(2020, 4, 23), 90 },
                    { new Guid("D464CB9A-97CD-58D3-B44D-65B7C9A471DA"), new Guid("20F8CCF8-94B1-418D-B923-C375B042BDA8"), "2.03.0", new DateOnly(2020, 6, 25), 100 },
                    { new Guid("ABE05F17-4E75-5535-A510-F7BAEB0AAE9A"), new Guid("20F8CCF8-94B1-418D-B923-C375B042BDA8"), "2.04.0", new DateOnly(2020, 8, 27), 110 },
                    { new Guid("69F15921-FBEE-5644-A7E6-6ACEB7F0A86B"), new Guid("20F8CCF8-94B1-418D-B923-C375B042BDA8"), "2.05.0", new DateOnly(2021, 1, 7), 120 },
                    { new Guid("20582567-C9E1-5F53-9968-33D65D223B36"), new Guid("20F8CCF8-94B1-418D-B923-C375B042BDA8"), "2.06.0", new DateOnly(2021, 4, 8), 130 },
                    { new Guid("0318DF73-6260-5C22-9627-799B11FD4BE6"), new Guid("20F8CCF8-94B1-418D-B923-C375B042BDA8"), "2.07.0", new DateOnly(2021, 7, 8), 140 },
                    { new Guid("E44259F2-87BE-56AF-B4CC-A4D6B59C489A"), new Guid("20F8CCF8-94B1-418D-B923-C375B042BDA8"), "2.08.0", new DateOnly(2022, 4, 21), 150 },
                    { new Guid("688DBB5C-35B5-556D-BD83-B048FB45EC04"), new Guid("00E93A6B-9C39-452F-96B0-1DF42DBDD0AC"), "1.00.0", new DateOnly(2016, 11, 13), 10 },
                    { new Guid("3D393EFD-A9BD-54A2-8A0E-519D42745A2F"), new Guid("00E93A6B-9C39-452F-96B0-1DF42DBDD0AC"), "1.01.0", new DateOnly(2017, 1, 20), 20 },
                    { new Guid("1C58C1AF-3F95-52C8-B47D-5CEC81549C12"), new Guid("00E93A6B-9C39-452F-96B0-1DF42DBDD0AC"), "1.02.0", new DateOnly(2017, 3, 27), 30 },
                    { new Guid("EFC1693A-640A-5EB4-AC66-2AE7A51B62E1"), new Guid("00E93A6B-9C39-452F-96B0-1DF42DBDD0AC"), "1.03.0", new DateOnly(2017, 4, 17), 40 },
                    { new Guid("6A1540EE-B4AB-5BD5-B538-7F02D7FD12B9"), new Guid("00E93A6B-9C39-452F-96B0-1DF42DBDD0AC"), "1.04.0", new DateOnly(2017, 5, 29), 50 },
                    { new Guid("9E05B384-C65D-5C2F-9C8A-76161DC4B46B"), new Guid("00E93A6B-9C39-452F-96B0-1DF42DBDD0AC"), "1.05.0", new DateOnly(2017, 6, 19), 60 },
                    { new Guid("1492F1F8-F85A-5419-B385-CB60209B2357"), new Guid("00E93A6B-9C39-452F-96B0-1DF42DBDD0AC"), "1.06.0", new DateOnly(2017, 7, 24), 70 },
                    { new Guid("D5B4A70C-AA9B-5F28-9DAE-45A7790D5D77"), new Guid("00E93A6B-9C39-452F-96B0-1DF42DBDD0AC"), "1.07.0", new DateOnly(2017, 7, 31), 80 },
                    { new Guid("21D477F9-7231-59BC-A538-145A792A364B"), new Guid("00E93A6B-9C39-452F-96B0-1DF42DBDD0AC"), "1.08.0", new DateOnly(2017, 9, 18), 90 },
                    { new Guid("6BCDFD64-767F-5D26-B809-54E8510546F2"), new Guid("00E93A6B-9C39-452F-96B0-1DF42DBDD0AC"), "1.09.0", new DateOnly(2017, 9, 25), 100 },
                    { new Guid("6B5B8FFF-90C8-5843-976D-3D57985F68F6"), new Guid("00E93A6B-9C39-452F-96B0-1DF42DBDD0AC"), "1.10.0", new DateOnly(2017, 11, 27), 110 },
                    { new Guid("E9EA0527-14C4-5802-8947-3A815D0C87DA"), new Guid("00E93A6B-9C39-452F-96B0-1DF42DBDD0AC"), "2.00.0", new DateOnly(2018, 1, 1), 120 },
                    { new Guid("F2BD6162-8116-5B1D-89C7-05BD22EDC4C6"), new Guid("00E93A6B-9C39-452F-96B0-1DF42DBDD0AC"), "2.01.0", new DateOnly(2018, 2, 26), 130 },
                    { new Guid("0178163A-5132-5C16-9DCB-2D59BC31645B"), new Guid("00E93A6B-9C39-452F-96B0-1DF42DBDD0AC"), "2.02.0", new DateOnly(2018, 3, 2), 140 },
                    { new Guid("41E6C309-57A8-566F-A73F-6C0FAE2EF592"), new Guid("00E93A6B-9C39-452F-96B0-1DF42DBDD0AC"), "2.03.0", new DateOnly(2018, 4, 30), 150 },
                    { new Guid("3ED829EB-62FD-59A8-B397-41FE67653B5D"), new Guid("00E93A6B-9C39-452F-96B0-1DF42DBDD0AC"), "2.04.0", new DateOnly(2018, 6, 25), 160 },
                    { new Guid("EDA184DD-5303-54E7-BF65-30ABABAE8E9D"), new Guid("00E93A6B-9C39-452F-96B0-1DF42DBDD0AC"), "2.05.0", new DateOnly(2018, 8, 27), 170 },
                    { new Guid("F8BE0A7D-4FC1-5476-9BD4-84910D32365C"), new Guid("D8316882-8D08-4993-B692-D0608392FB02"), "1.00.0", new DateOnly(2014, 12, 13), 10 },
                    { new Guid("FD0D2CC8-A552-57D0-AA16-B34D8FD96345"), new Guid("D8316882-8D08-4993-B692-D0608392FB02"), "1.01.0", null, 20 },
                    { new Guid("33D72B37-0E4F-5722-8F30-E805FF0220FB"), new Guid("D8316882-8D08-4993-B692-D0608392FB02"), "1.02.0", null, 30 },
                    { new Guid("F7CA759C-61FB-593D-9FB7-BAE37DB7BFB2"), new Guid("D8316882-8D08-4993-B692-D0608392FB02"), "1.03.0", null, 40 },
                    { new Guid("09475AD0-9EC0-5E25-8AE2-DC95A8A376F9"), new Guid("D8316882-8D08-4993-B692-D0608392FB02"), "1.04.0", null, 50 },
                    { new Guid("38E53D19-D7E4-584B-ADCC-B5465E62BAA3"), new Guid("D8316882-8D08-4993-B692-D0608392FB02"), "1.05.0", null, 60 },
                    { new Guid("091F2DC0-D882-5520-8F1C-F70B20B8CB65"), new Guid("D8316882-8D08-4993-B692-D0608392FB02"), "1.06.0", null, 70 },
                    { new Guid("D5D9DA7D-844B-5BB1-8E6D-898E1B3EB13C"), new Guid("D8316882-8D08-4993-B692-D0608392FB02"), "1.07.0", null, 80 },
                    { new Guid("718ABFA5-67E0-5546-B9EB-D652BC2345A0"), new Guid("D8316882-8D08-4993-B692-D0608392FB02"), "1.08.0", null, 90 },
                    { new Guid("271232F0-BC38-5573-8150-B3BCCB132A28"), new Guid("D8316882-8D08-4993-B692-D0608392FB02"), "1.09.0", null, 100 },
                    { new Guid("FE16C676-B06A-5458-B062-1A6E07C608E6"), new Guid("D8316882-8D08-4993-B692-D0608392FB02"), "1.10.0", null, 110 },
                    { new Guid("E34251B4-3410-598A-A6E1-FB354936320D"), new Guid("D8316882-8D08-4993-B692-D0608392FB02"), "1.11.0", null, 120 },
                    { new Guid("7188F0C6-7810-5C62-A715-2309E2B0AE91"), new Guid("D8316882-8D08-4993-B692-D0608392FB02"), "1.12.0", null, 130 },
                    { new Guid("D8E2E62F-6F50-5212-82A3-E7F85E932598"), new Guid("D8316882-8D08-4993-B692-D0608392FB02"), "1.13.0", null, 140 },
                    { new Guid("0784CACA-86C0-5B95-BD4B-66123B24D4EB"), new Guid("D8316882-8D08-4993-B692-D0608392FB02"), "1.14.0", null, 150 },
                    { new Guid("9F79CBC2-6041-5432-819D-1D087A9B1369"), new Guid("D8316882-8D08-4993-B692-D0608392FB02"), "1.15.0", null, 160 },
                    { new Guid("D3E08849-86EE-5E7A-AB1C-FEFDE9467087"), new Guid("D8316882-8D08-4993-B692-D0608392FB02"), "1.16.0", null, 170 },
                    { new Guid("7D049ACD-3C62-5BDB-ABE6-149320971BD1"), new Guid("D8316882-8D08-4993-B692-D0608392FB02"), "1.17.0", null, 180 },
                    { new Guid("17CC17A8-DBA6-5715-9B29-3B7548727227"), new Guid("D8316882-8D08-4993-B692-D0608392FB02"), "1.18.0", null, 190 },
                    { new Guid("2B5B4279-DC6A-5F88-B9DB-A9559508069B"), new Guid("D8316882-8D08-4993-B692-D0608392FB02"), "1.19.0", null, 200 },
                    { new Guid("993BD9D3-503A-5661-89CE-4DA7A822C839"), new Guid("D8316882-8D08-4993-B692-D0608392FB02"), "1.20.0", null, 210 },
                    { new Guid("10A3E556-A945-5A84-9AA1-5F91652823EF"), new Guid("D8316882-8D08-4993-B692-D0608392FB02"), "1.21.0", null, 220 },
                    { new Guid("73E434FB-47BF-542E-A583-9A7E776983E0"), new Guid("363B8D21-2DDE-4CE0-A54E-2AEE2B7280A2"), "Pre-v1.10", new DateOnly(2013, 1, 30), 10 },
                    { new Guid("BBF783DD-1822-5BD4-9A2F-FFDD14654066"), new Guid("363B8D21-2DDE-4CE0-A54E-2AEE2B7280A2"), "1.10", null, 20 },
                    { new Guid("7E1BCF5E-5CE1-5DDC-AA23-D73CBAE690D0"), new Guid("E172B206-ACF9-4A52-A6FE-CBF56FE15167"), "1.00", new DateOnly(2012, 11, 24), 10 },
                    { new Guid("DA06C8D7-E8DE-5427-AB31-EEFDB2E4A9A3"), new Guid("E172B206-ACF9-4A52-A6FE-CBF56FE15167"), "1.01", null, 20 },
                    { new Guid("794E1E61-D609-5FB3-A77A-99C3BE073F81"), new Guid("E172B206-ACF9-4A52-A6FE-CBF56FE15167"), "1.10", null, 30 },
                    { new Guid("DDB0813B-F810-5BA0-8BE5-7CBA28D71DEF"), new Guid("E172B206-ACF9-4A52-A6FE-CBF56FE15167"), "1.20", null, 40 },
                    { new Guid("93C7FDBE-F5D5-51B2-96A2-077FF3D64869"), new Guid("E172B206-ACF9-4A52-A6FE-CBF56FE15167"), "1.30", null, 50 },
                    { new Guid("0569987F-ED9C-592B-880F-D77132EC33C6"), new Guid("E172B206-ACF9-4A52-A6FE-CBF56FE15167"), "1.40", null, 60 },
                    { new Guid("9C8C8EBE-EFB0-5157-9183-84DB27AEE7F5"), new Guid("E172B206-ACF9-4A52-A6FE-CBF56FE15167"), "1.50", null, 70 },
                    { new Guid("42E4DECC-7D40-534E-A181-30FCCE7667E5"), new Guid("E172B206-ACF9-4A52-A6FE-CBF56FE15167"), "1.51", null, 80 },
                    { new Guid("DF17EEEF-4C24-521D-8C3B-138FEAC5636D"), new Guid("E172B206-ACF9-4A52-A6FE-CBF56FE15167"), "1.60", null, 90 },
                    { new Guid("4FB9C454-809A-5C2D-8C7C-314E440D8B05"), new Guid("E172B206-ACF9-4A52-A6FE-CBF56FE15167"), "1.61", null, 100 },
                    { new Guid("54D68C2B-BBCD-5587-B445-7B836BF3ED1C"), new Guid("90C0A1E0-0DE6-4D05-A035-533669224482"), "1.00", new DateOnly(2011, 1, 22), 10 },
                    { new Guid("C51D8605-F507-53F0-91DE-33EBF59792FD"), new Guid("90C0A1E0-0DE6-4D05-A035-533669224482"), "1.10", null, 20 },
                    { new Guid("ABE7DE82-5A12-5627-BF91-C4B068A485E7"), new Guid("90C0A1E0-0DE6-4D05-A035-533669224482"), "1.20", null, 30 },
                    { new Guid("AC42106A-76DF-5EAE-ABCA-ED8BB4F8643D"), new Guid("90C0A1E0-0DE6-4D05-A035-533669224482"), "1.30", null, 40 },
                    { new Guid("35C0F068-801C-5F85-BFB8-A46895BDDB06"), new Guid("90C0A1E0-0DE6-4D05-A035-533669224482"), "1.40", null, 50 },
                    { new Guid("4E45B6C7-E5EC-5C2F-91A1-33152B84F5E6"), new Guid("90C0A1E0-0DE6-4D05-A035-533669224482"), "1.50", null, 60 },
                    { new Guid("CBD34229-8127-586D-8AB1-F3607AB2FE69"), new Guid("90C0A1E0-0DE6-4D05-A035-533669224482"), "1.51", null, 70 },
                    { new Guid("39D650EA-2C77-56C7-B5FA-FF9050261B38"), new Guid("178562FC-740F-46C6-B957-0A0381CCCFC4"), "1.01", new DateOnly(2010, 3, 6), 10 },
                    { new Guid("7C0C9057-F389-5B34-958A-7699030D7D43"), new Guid("178562FC-740F-46C6-B957-0A0381CCCFC4"), "1.02", null, 20 },
                    { new Guid("D9DB0B12-1811-5F85-85FC-39192EAFEB24"), new Guid("178562FC-740F-46C6-B957-0A0381CCCFC4"), "1.03", null, 30 },
                    { new Guid("C5E83DCA-7519-51F8-82F0-ACF4D7CEBE6B"), new Guid("178562FC-740F-46C6-B957-0A0381CCCFC4"), "1.04", null, 40 },
                    { new Guid("DB07C3AC-1E09-50E6-962F-0FAE6E0CC6BA"), new Guid("178562FC-740F-46C6-B957-0A0381CCCFC4"), "1.05", null, 50 },
                    { new Guid("825C912B-B016-5014-A9E9-29653E6D32C7"), new Guid("178562FC-740F-46C6-B957-0A0381CCCFC4"), "1.06", null, 60 },
                    { new Guid("97B18E02-3C0B-52B7-98B3-BD3535F91992"), new Guid("178562FC-740F-46C6-B957-0A0381CCCFC4"), "1.07", null, 70 },
                    { new Guid("0253C38A-396C-571F-BFBF-EFF1CDD11CA9"), new Guid("178562FC-740F-46C6-B957-0A0381CCCFC4"), "1.10", null, 80 },
                    { new Guid("E7C67EDB-4B4C-5C35-A9B5-BF340138D914"), new Guid("178562FC-740F-46C6-B957-0A0381CCCFC4"), "1.20", null, 90 },
                    { new Guid("C56130AD-A0B8-5DB5-AB86-1CF584D0FCC9"), new Guid("D4C22342-F0EA-4F8F-9C5B-BE75ACC980FA"), "Release", new DateOnly(2008, 11, 25), 10 },
                    { new Guid("91842DA1-496C-50F3-9810-98DCFEDA6C0F"), new Guid("DF15FB43-5E13-4941-A7AE-D979F8FD6220"), "Release", new DateOnly(2007, 12, 14), 10 },
                    { new Guid("D6EE1651-EFF0-5774-A023-9AE0146BD29F"), new Guid("07CB82DD-D577-41EA-BA9E-9746061752C1"), "1.05", new DateOnly(2006, 12, 15), 10 },
                    { new Guid("3AEEEBAD-D79F-5F11-A3F3-D31C26C42EED"), new Guid("07CB82DD-D577-41EA-BA9E-9746061752C1"), "1.08", null, 20 },
                    { new Guid("B03B1175-B29F-5AD6-82C7-240B5AE69D52"), new Guid("4A18B364-4B9D-42F3-AE79-222CF1D4ED7B"), "Release", new DateOnly(2006, 1, 28), 10 },
                    { new Guid("47AB0B99-429B-5727-88A3-C3E52DD33E58"), new Guid("4B9842C7-EE1B-4B0E-A370-9A966994236A"), "Release", new DateOnly(2004, 11, 30), 10 },
                    { new Guid("2794E58F-0760-51AE-ABF4-1325A8CC1CC8"), new Guid("69D234A7-4141-4A69-AC55-114B7164198D"), "Release", new DateOnly(2004, 4, 2), 10 },
                    { new Guid("2FC0F792-813A-58B1-83B7-9549D416CD9A"), new Guid("94BD6973-8CEC-48D7-AFF2-B310B3B0B0FE"), "Release", new DateOnly(2003, 10, 4), 10 },
                    { new Guid("682E9FBD-2A36-58A5-B695-D9FCD0BB09DA"), new Guid("A409D148-8167-4065-A351-5EC45A863F1A"), "Release", new DateOnly(2003, 5, 11), 10 },
                    { new Guid("366AE17C-B20F-5DFC-A07C-85D9FF66AD80"), new Guid("953CC701-4A64-4E4B-BBB3-51C7D66BDAE6"), "Release", new DateOnly(2002, 11, 23), 10 },
                    { new Guid("F4E5BA2A-5314-5524-9743-A3C5FB71EF74"), new Guid("C995A044-E897-4730-B8E9-599B822BCA0D"), "Release", new DateOnly(2002, 3, 9), 10 },
                    { new Guid("76EB501E-48FE-58E2-A121-F6777D983D97"), new Guid("CE37A838-2CAD-40F4-ACC0-A67D6FB97239"), "Release", new DateOnly(2002, 1, 10), 10 },
                    { new Guid("9F97D685-C756-518D-8FAA-8F3241F7722C"), new Guid("084B06F5-5E8A-47BC-8307-442DB8000C5B"), "Release", new DateOnly(2001, 11, 1), 10 },
                    { new Guid("557D1A70-2E0E-532B-B998-75CC56333D24"), new Guid("FD9A0B6A-F241-47A0-980A-F7CB518A8081"), "Release", new DateOnly(2001, 6, 1), 10 },
                    { new Guid("61AC948C-D8D6-571C-938B-9695D2AAEE6E"), new Guid("84562821-C87E-4346-B0C1-38A7DFA5637F"), "Release", new DateOnly(2001, 1, 20), 10 },
                    { new Guid("83B54EBC-1EF1-563F-959D-B5942BDD02E6"), new Guid("F680D1E5-C4F8-4479-8423-CBF59C1512D6"), "Release", new DateOnly(2000, 12, 7), 10 },
                    { new Guid("6712EB83-F46A-5365-A74D-91A3543BE964"), new Guid("34CEB319-84FA-4F2D-A48C-98DC861DA3FB"), "Release", new DateOnly(2000, 11, 14), 10 },
                    { new Guid("BCA061D1-8AB5-552E-9D74-18D5E2AA4AF2"), new Guid("38D59ECF-F5E0-42A3-9111-796EB398FFEB"), "Release", new DateOnly(2000, 9, 3), 10 },
                    { new Guid("FC738A75-CA3A-5739-AFB8-4C6B5408D611"), new Guid("72A67D8A-DD28-470D-9857-CDE789BCAFD7"), "Release", new DateOnly(2000, 5, 7), 10 },
                    { new Guid("8EC504AB-2E9A-554F-9889-771F12BD4F06"), new Guid("6558B48D-9EF2-4A51-BC0E-8A0956469D01"), "Release", new DateOnly(1999, 12, 27), 10 },
                    { new Guid("2F5D5278-8045-57DE-907D-5CEDE7376BBC"), new Guid("4FDCE23C-904C-4538-952F-DDA636D1B154"), "Release", new DateOnly(1999, 9, 20), 10 },
                    { new Guid("A5834852-C212-5BBB-AE20-13F6796AAE29"), new Guid("D8316882-8D08-4993-B692-D0608392FB02"), "JE", null, 230 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChartMix_MixVersion_AddedInVersionId",
                schema: "scores",
                table: "ChartMix");

            migrationBuilder.DropTable(
                name: "MixVersion",
                schema: "scores");

            migrationBuilder.DropIndex(
                name: "IX_ChartMix_AddedInVersionId",
                schema: "scores",
                table: "ChartMix");

            migrationBuilder.DropColumn(
                name: "AddedInVersionId",
                schema: "scores",
                table: "ChartMix");
        }
    }
}
