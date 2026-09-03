using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AvatarCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Avatar",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Mixes = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Avatar", x => x.Id);
                });

            // 182 rows, 170 groups: one row per distinct PICTURE, seeded from the three
            // official avatar pages (docs/design/avatar-selection.md §3). The 412 entries those
            // pages list collapse to this by pixel comparison, not by name or filename — the two
            // Phoenix directories reuse ids for unrelated art, so 4f6176... is Azura under
            // /avatar_img/ and Electra under /avatar_img2/.
            //
            // Names repeat on purpose. Phoenix 2 lists "Electra" twice, and both mixes list
            // Hero/hero and Miya/MIYA, all of which are genuinely different pictures. Nothing
            // downstream keys off the name.
            //
            // Mixes is a bitmask of 1 << (int)MixEnum: XX = 1, Phoenix = 2, Phoenix 2 = 4.
            migrationBuilder.Sql(@"
INSERT INTO scores.Avatar (GroupId, Name, ImageUrl, Mixes, SortOrder) VALUES
    (1, N'3D Alien Cat', N'https://piuimages.arroweclip.se/avatars/p2/ba58494e4209f88d185a1b8078aa66da.png', 6, 1),
    (2, N'ABT-1', N'https://piuimages.arroweclip.se/avatars/p2/7c9cae2720b196047ee881b934c3870a.png', 7, 2),
    (3, N'Ailee', N'https://piuimages.arroweclip.se/avatars/p2/6afa50fff54b07adb0eaf8bfa841e05c.png', 7, 3),
    (4, N'Aki & Erue', N'https://piuimages.arroweclip.se/avatars/p2/ac3ff7cb65ee80c99ff09f4354f1674b.png', 7, 4),
    (5, N'Alic!ta', N'https://piuimages.arroweclip.se/avatars/p2/1df54cb299773ae8dfda2959e76e5009.png', 6, 5),
    (6, N'Alice', N'https://piuimages.arroweclip.se/avatars/p2/e0dd6b4234ed867c128ae26a16b75b04.png', 7, 6),
    (7, N'Alien Pig', N'https://piuimages.arroweclip.se/avatars/p2/2c862079eee6be3bac006f385a683460.png', 7, 7),
    (8, N'Alter ego', N'https://piuimages.arroweclip.se/avatars/p2/306444d5a305a3662a04d07613042865.png', 6, 8),
    (9, N'Althera', N'https://piuimages.arroweclip.se/avatars/p2/27db599503b6bd92d0dfc1b6cf5dafab.png', 6, 9),
    (10, N'AM Corporation Research Team', N'https://piuimages.arroweclip.se/avatars/p2/eba954f0f25b5f67e1fbd8665c1586ee.png', 4, 10),
    (10, N'AM Corporation Research Team', N'https://piuimages.arroweclip.se/avatars/e40030248735bd374bb35d00d6a67a31.png', 2, 10),
    (11, N'Amami Satoko', N'https://piuimages.arroweclip.se/avatars/p2/4ecfdcfe096e5d5f2ac973b0522aa274.png', 7, 11),
    (12, N'Amil', N'https://piuimages.arroweclip.se/avatars/p2/a5ac58c84170a622f8adb8c4e8e30257.png', 7, 12),
    (13, N'Arca', N'https://piuimages.arroweclip.se/avatars/p2/6992859b696b2a0e4bd5b607454a5906.png', 7, 13),
    (14, N'Aria', N'https://piuimages.arroweclip.se/avatars/p2/767d1a3db8c5b1dbb6ea631cf8684acb.png', 6, 14),
    (15, N'Armillary Sphere', N'https://piuimages.arroweclip.se/avatars/p2/b9c0e4060ec640614964c6ec3b9ec3d7.png', 6, 15),
    (16, N'Assistant Admin', N'https://piuimages.arroweclip.se/avatars/p2/7c21a530a20ebfb81431a0ee1b50364c.png', 6, 16),
    (17, N'Aya', N'https://piuimages.arroweclip.se/avatars/p2/ce0e9f464be0c2b2d01adccfbab031f8.png', 7, 17),
    (18, N'Ayumu', N'https://piuimages.arroweclip.se/avatars/p2/8f47a1af5f7065226a073dc7ee278e84.png', 6, 18),
    (19, N'Azura', N'https://piuimages.arroweclip.se/avatars/p2/066f504c65fd3611e081b2a70c39c147.png', 4, 19),
    (19, N'Azura', N'https://piuimages.arroweclip.se/avatars/4f617606e7751b2dc2559d80f09c40bf.png', 2, 19),
    (20, N'Beethoven'' Previous life', N'https://piuimages.arroweclip.se/avatars/p2/d6caab7ea7c0d48a9bfb78aff3000a5d.png', 6, 20),
    (21, N'Berry', N'https://piuimages.arroweclip.se/avatars/p2/9fe9b4d9676344500d74aead729e4770.png', 6, 21),
    (22, N'CanCan Rangers', N'https://piuimages.arroweclip.se/avatars/p2/3b1f557fa939dbb0443dcaa8748ae11b.png', 7, 22),
    (23, N'Century of Limitation', N'https://piuimages.arroweclip.se/avatars/p2/205a409f6fbe7cbd6130d6084e65b491.png', 7, 23),
    (24, N'Christos T Elias', N'https://piuimages.arroweclip.se/avatars/p2/eb97120913378496d00f0332453a863c.png', 7, 24),
    (25, N'CIDER', N'https://piuimages.arroweclip.se/avatars/p2/45cbafcb6d086d5b0d10c09b95965ab1.png', 4, 25),
    (25, N'CIDER', N'https://piuimages.arroweclip.se/avatars/e39bc9c053ae6428c55be2239a5ae725.png', 2, 25),
    (26, N'Clara', N'https://piuimages.arroweclip.se/avatars/p2/f039ec2acf0e57da779feab8432c53aa.png', 6, 26),
    (27, N'Clotho', N'https://piuimages.arroweclip.se/avatars/p2/89ed1c4662baae04fc8f30f992aa3110.png', 7, 27),
    (28, N'CoCo', N'https://piuimages.arroweclip.se/avatars/p2/adc053f1797b03b8d6348f5af65d5dea.png', 6, 28),
    (29, N'Dalpros', N'https://piuimages.arroweclip.se/avatars/p2/26860366a60c910aa9e95f430926f855.png', 6, 29),
    (30, N'Dasom', N'https://piuimages.arroweclip.se/avatars/p2/7d91cd79d7304d7b8ba1ae5479b6cf75.png', 7, 30),
    (31, N'DENEBOLA', N'https://piuimages.arroweclip.se/avatars/p2/514210469242393201a5b8986f341cd8.png', 7, 31),
    (32, N'Denis', N'https://piuimages.arroweclip.se/avatars/p2/910a2d692b335d5ae4f641d693afa5f5.png', 7, 32),
    (33, N'Detective Jupin', N'https://piuimages.arroweclip.se/avatars/p2/0015ee3f24b5766ad193eee77d6e2a4e.png', 6, 33),
    (34, N'Devit', N'https://piuimages.arroweclip.se/avatars/p2/0ca2fb923c7f6d173feb8ae38996053f.png', 7, 34),
    (35, N'Devit Showdown', N'https://piuimages.arroweclip.se/avatars/p2/2f52b0799bb4a61064983ca8237cd317.png', 6, 35),
    (36, N'Devit''s Despair', N'https://piuimages.arroweclip.se/avatars/p2/a3b9583104258d96d9b163d03e749eec.png', 6, 36),
    (37, N'Devit''s Hope', N'https://piuimages.arroweclip.se/avatars/p2/59cee2e0df4c4be1d318e986d491cc47.png', 4, 37),
    (37, N'Devit''s Hope', N'https://piuimages.arroweclip.se/avatars/1df54cb299773ae8dfda2959e76e5009.png', 2, 37),
    (38, N'Downi', N'https://piuimages.arroweclip.se/avatars/p2/174eccf2c98ec77dfb83d8b53b38f902.png', 4, 38),
    (38, N'Downi', N'https://piuimages.arroweclip.se/avatars/3d930dcd746314e94542b763a2805504.png', 2, 38),
    (39, N'Eleanor', N'https://piuimages.arroweclip.se/avatars/p2/33ecd96b847c0f8433ca999e63ba6c75.png', 6, 39),
    (40, N'Electra', N'https://piuimages.arroweclip.se/avatars/p2/5f71d39281932f4428cf0fd135a35d72.png', 4, 40),
    (41, N'Electra', N'https://piuimages.arroweclip.se/avatars/p2/4f617606e7751b2dc2559d80f09c40bf.png', 4, 41),
    (42, N'Electronic Cat', N'https://piuimages.arroweclip.se/avatars/p2/b7feb4f1758089808a9d73ec45ba6df0.png', 7, 42),
    (43, N'Emily', N'https://piuimages.arroweclip.se/avatars/p2/7d83b199e654fb10f2bbb09fff0bf5c8.png', 7, 43),
    (44, N'Escape', N'https://piuimages.arroweclip.se/avatars/p2/0b88ff1250d7520bbeb6c1ee355dd791.png', 7, 44),
    (45, N'Everybody Loves Chicken', N'https://piuimages.arroweclip.se/avatars/p2/e947839f4dabdcbf70388e06165b5dbd.png', 7, 45),
    (46, N'Excessive drinking', N'https://piuimages.arroweclip.se/avatars/p2/5449f88cb052ab1dd2908c33fc0766b2.png', 6, 46),
    (47, N'Executing the Apocalypse', N'https://piuimages.arroweclip.se/avatars/p2/ce319fff536d3ecec1088e64693a2f5f.png', 6, 47),
    (48, N'Explosion of passion', N'https://piuimages.arroweclip.se/avatars/p2/6d256dba90fbc01f3c9228f94f3023f0.png', 6, 48),
    (49, N'Freedom of the Dead', N'https://piuimages.arroweclip.se/avatars/p2/2393f33b3d8dc6b4ec03fb7d3b7ce845.png', 7, 49),
    (50, N'fuyu', N'https://piuimages.arroweclip.se/avatars/p2/9b4be2c8f25ab1c2e89442bae71164bb.png', 7, 50),
    (51, N'Gargoyle', N'https://piuimages.arroweclip.se/avatars/p2/baf2178281bd447ce8424ad965131894.png', 7, 51),
    (52, N'General Admin', N'https://piuimages.arroweclip.se/avatars/p2/8f820f8a6c5457264ea6b4e4b9b8ccbc.png', 6, 52),
    (53, N'Ghost Tree', N'https://piuimages.arroweclip.se/avatars/p2/1714cb21061955169a6e39b765339a7a.png', 6, 53),
    (54, N'Gran', N'https://piuimages.arroweclip.se/avatars/p2/59fa5655e1c010800677e43911822570.png', 6, 54),
    (55, N'Great Teachers', N'https://piuimages.arroweclip.se/avatars/p2/56c51d772d63baa4ddc385090a498dcc.png', 7, 55),
    (56, N'Guild Savior', N'https://piuimages.arroweclip.se/avatars/p2/af39c915995c6ebc88bbfb978e02c3a7.png', 7, 56),
    (57, N'Hanna', N'https://piuimages.arroweclip.se/avatars/p2/901ceb863fc221f3e704143c7177e1b3.png', 7, 57),
    (58, N'Hannah', N'https://piuimages.arroweclip.se/avatars/p2/c4e46603a5c931efda0dfe3945000b37.png', 7, 58),
    (59, N'Hellfire', N'https://piuimages.arroweclip.se/avatars/p2/990ccbdf6fed014063980ddf93419faf.png', 7, 59),
    (60, N'Hercules', N'https://piuimages.arroweclip.se/avatars/p2/1d9ff5173d639249d63ee9901637b2cb.png', 6, 60),
    (61, N'Hero', N'https://piuimages.arroweclip.se/avatars/p2/204ecece3614788dec728d8e817949d8.png', 7, 61),
    (62, N'hero', N'https://piuimages.arroweclip.se/avatars/p2/07c16aaee00d066c17f388653f375509.png', 7, 62),
    (63, N'Hibiscus', N'https://piuimages.arroweclip.se/avatars/p2/41933c642e404a1f1abece4a6185cfb6.png', 6, 63),
    (64, N'Hidden Fortress', N'https://piuimages.arroweclip.se/avatars/p2/34125cc217989582d2d80c2051961154.png', 6, 64),
    (65, N'Human Faced Being', N'https://piuimages.arroweclip.se/avatars/p2/0f5cbf9469906bbdb4a07deb62d02b4a.png', 7, 65),
    (66, N'HWANG DONG', N'https://piuimages.arroweclip.se/avatars/p2/f8cbb833459f7737016a587dc0e21b5c.png', 6, 66),
    (67, N'Jack P. MURDOCH', N'https://piuimages.arroweclip.se/avatars/p2/7943a785cb4372305284879af49cdbc3.png', 6, 67),
    (68, N'Jeanne', N'https://piuimages.arroweclip.se/avatars/p2/b1fcbf39b1aec9779ec42166fc8c6824.png', 4, 68),
    (68, N'Jeanne', N'https://piuimages.arroweclip.se/avatars/4ecfdcfe096e5d5f2ac973b0522aa274.png', 3, 68),
    (69, N'Jonathan', N'https://piuimages.arroweclip.se/avatars/p2/4c38402fbef1c19aaa8f3157d4f7ae13.png', 7, 69),
    (70, N'Jordan', N'https://piuimages.arroweclip.se/avatars/p2/6b0cc0ae9243bb3c5c91931db9c3d92b.png', 6, 70),
    (71, N'JRV-042', N'https://piuimages.arroweclip.se/avatars/p2/c1b8599053c4797ff01025a4cb3574f6.png', 6, 71),
    (72, N'Judgment of the ''Motte''', N'https://piuimages.arroweclip.se/avatars/p2/45a87f2672521f65c49bf603447e7357.png', 6, 72),
    (73, N'King Scorpion', N'https://piuimages.arroweclip.se/avatars/p2/87f3e27e545447b14c9a5fa6c31dc475.png', 7, 73),
    (74, N'Kiro & Kira', N'https://piuimages.arroweclip.se/avatars/p2/44939ce38e58a0e852f518f321efadaf.png', 6, 74),
    (75, N'KUGUTSU', N'https://piuimages.arroweclip.se/avatars/p2/ca28fe88daf82a3aae0aa9d44ddb5afd.png', 6, 75),
    (76, N'Kumomo', N'https://piuimages.arroweclip.se/avatars/p2/e40030248735bd374bb35d00d6a67a31.png', 4, 76),
    (76, N'Kumomo', N'https://piuimages.arroweclip.se/avatars/be67afb724f7176d1888f87b46e01066.png', 2, 76),
    (77, N'Kyouka', N'https://piuimages.arroweclip.se/avatars/p2/f1bfe263a3db5354d319ca4fc052f8aa.png', 6, 77),
    (78, N'Lacie', N'https://piuimages.arroweclip.se/avatars/p2/4cd29544336be400a4e06251c5d39b1c.png', 6, 78),
    (79, N'Laimu', N'https://piuimages.arroweclip.se/avatars/p2/02dcbe95815975c92aa48b065fa54b5b.png', 6, 79),
    (80, N'Lightning', N'https://piuimages.arroweclip.se/avatars/p2/a0277469778f8fe0282f4fdc84db1b9c.png', 4, 80),
    (80, N'Lightning', N'https://piuimages.arroweclip.se/avatars/a3b9583104258d96d9b163d03e749eec.png', 2, 80),
    (81, N'Lightning Green', N'https://piuimages.arroweclip.se/avatars/p2/38be9d13894a90c796c6dba77a72c49d.png', 7, 81),
    (82, N'Little Lamb', N'https://piuimages.arroweclip.se/avatars/p2/901d48ef538745431eaa94cef7676758.png', 7, 82),
    (83, N'Loar', N'https://piuimages.arroweclip.se/avatars/p2/3d930dcd746314e94542b763a2805504.png', 7, 83),
    (84, N'Loser MURDOCH', N'https://piuimages.arroweclip.se/avatars/p2/484172adaa042f3cec843c4c253463a9.png', 6, 84),
    (85, N'Luana', N'https://piuimages.arroweclip.se/avatars/p2/75f90236ea58c1c27f5dda7ae85c0d10.png', 4, 85),
    (85, N'Luana', N'https://piuimages.arroweclip.se/avatars/45cbafcb6d086d5b0d10c09b95965ab1.png', 2, 85),
    (86, N'Lucent', N'https://piuimages.arroweclip.se/avatars/p2/5cadcb1cf596b499b579f39001c5bf07.png', 6, 86),
    (87, N'Lyra D. Fersen', N'https://piuimages.arroweclip.se/avatars/p2/d24da21a69e2b4a267d43cdaa88fd015.png', 7, 87),
    (88, N'Lyra, the Awaken', N'https://piuimages.arroweclip.se/avatars/p2/007b34c60785c9f12ec8473e354402f6.png', 7, 88),
    (89, N'Lyrica', N'https://piuimages.arroweclip.se/avatars/p2/55c827f5c851b85c5e3b3c896932fa52.png', 6, 89),
    (90, N'Malaventurados', N'https://piuimages.arroweclip.se/avatars/xx/107.png', 1, 90),
    (91, N'Mari', N'https://piuimages.arroweclip.se/avatars/p2/7aae77ee552c2c0985c678778ceda0bd.png', 7, 91),
    (92, N'Matsuri', N'https://piuimages.arroweclip.se/avatars/p2/148ed61b8a006a930286c8cbacc5eb65.png', 7, 92),
    (93, N'Matsuri (HEY)', N'https://piuimages.arroweclip.se/avatars/p2/6a9a7ebdd28cb686c36fac43b55754fd.png', 7, 93),
    (94, N'Maya', N'https://piuimages.arroweclip.se/avatars/p2/0809327fb056c528294a1d20e22760d1.png', 6, 94),
    (95, N'Meiling & Hao-Yu', N'https://piuimages.arroweclip.se/avatars/xx/140.png', 1, 95),
    (96, N'Melissa', N'https://piuimages.arroweclip.se/avatars/p2/b6d9fd3c725ed872e35c455f53544943.png', 7, 96),
    (97, N'Melt', N'https://piuimages.arroweclip.se/avatars/p2/cd5a74ed10b9e84620882b501fe3797b.png', 4, 97),
    (97, N'Melt', N'https://piuimages.arroweclip.se/avatars/0b88ff1250d7520bbeb6c1ee355dd791.png', 2, 97),
    (98, N'Mental Cube', N'https://piuimages.arroweclip.se/avatars/p2/0ef99263b1db831b138aa50a1de1a409.png', 7, 98),
    (99, N'Meow', N'https://piuimages.arroweclip.se/avatars/xx/077.png', 1, 99),
    (100, N'MERKER', N'https://piuimages.arroweclip.se/avatars/p2/bc24509984b53ee82c23578d6ce16e44.png', 7, 100),
    (101, N'Meteor', N'https://piuimages.arroweclip.se/avatars/p2/d8567e37c73dcac59a568c75c260d1a1.png', 7, 101),
    (102, N'Michael', N'https://piuimages.arroweclip.se/avatars/p2/7e48efd1edbffa7926364726dd92fb01.png', 6, 102),
    (103, N'Mikazuki', N'https://piuimages.arroweclip.se/avatars/d24da21a69e2b4a267d43cdaa88fd015.png', 3, 103),
    (104, N'MingMing', N'https://piuimages.arroweclip.se/avatars/p2/69bf5806515af7919e1a9dd76246e3d9.png', 7, 104),
    (105, N'Mir', N'https://piuimages.arroweclip.se/avatars/p2/02e4f84bca65611e254a134cd864971c.png', 7, 105),
    (106, N'MIYA', N'https://piuimages.arroweclip.se/avatars/p2/3ca47f5df09ac01a7678df53d39533e6.png', 7, 106),
    (107, N'Miya', N'https://piuimages.arroweclip.se/avatars/p2/77ebac59a8a18e91a62c580b546797cc.png', 7, 107),
    (108, N'Mode G', N'https://piuimages.arroweclip.se/avatars/p2/876697a6cbfb2388981a4285b130638d.png', 7, 108),
    (109, N'MoMo & Rua', N'https://piuimages.arroweclip.se/avatars/p2/be67afb724f7176d1888f87b46e01066.png', 6, 109),
    (110, N'Munchkins', N'https://piuimages.arroweclip.se/avatars/p2/062d771f9ce22260f0a39f29b7256e6f.png', 6, 110),
    (111, N'Music Warrior', N'https://piuimages.arroweclip.se/avatars/p2/43d10b867ed9653cadc1b3218e52dfbe.png', 7, 111),
    (112, N'Na YuRee', N'https://piuimages.arroweclip.se/avatars/p2/f9bf2e38ba1894b9ec9f7c74b0e64142.png', 6, 112),
    (113, N'NaNa', N'https://piuimages.arroweclip.se/avatars/p2/e39bc9c053ae6428c55be2239a5ae725.png', 6, 113),
    (114, N'Nashor. Laurence', N'https://piuimages.arroweclip.se/avatars/p2/2af9cc524bc5e63a03706b093344fee8.png', 7, 114),
    (115, N'Natalia', N'https://piuimages.arroweclip.se/avatars/p2/b4ff9b32acb099631c93c60b031271f4.png', 6, 115),
    (116, N'Native Magic', N'https://piuimages.arroweclip.se/avatars/p2/7da9639b2ed2e16d06621734d80946af.png', 7, 116),
    (117, N'Neon Rocket', N'https://piuimages.arroweclip.se/avatars/p2/2a0d7b807254b39e58039addbbd8d2ab.png', 6, 117),
    (118, N'Onpiov', N'https://piuimages.arroweclip.se/avatars/p2/1c99229e0c024d45ec3b35317ee5730a.png', 7, 118),
    (119, N'Pale Rider', N'https://piuimages.arroweclip.se/avatars/p2/5e72ea75748c6c3f010ed856d1614105.png', 6, 119),
    (120, N'PanPan', N'https://piuimages.arroweclip.se/avatars/p2/6ed01094850e66d34aa4831f567363d4.png', 7, 120),
    (121, N'Paper Farmer', N'https://piuimages.arroweclip.se/avatars/p2/634fc8494577c9bd57b8f59683398751.png', 4, 121),
    (121, N'Paper Farmer', N'https://piuimages.arroweclip.se/avatars/f039ec2acf0e57da779feab8432c53aa.png', 2, 121),
    (122, N'Phantom', N'https://piuimages.arroweclip.se/avatars/p2/82916c6df504442fb3affe9228d98510.png', 6, 122),
    (123, N'Phantom Thief M & Detective P', N'https://piuimages.arroweclip.se/avatars/p2/41b7d75b8f2ef1be9e16a1acae6bdd4e.png', 4, 123),
    (123, N'Phantom Thief M & Detective P', N'https://piuimages.arroweclip.se/avatars/174eccf2c98ec77dfb83d8b53b38f902.png', 2, 123),
    (124, N'Phou', N'https://piuimages.arroweclip.se/avatars/p2/b82ad4ad4c15026be6ef5c4a542d0539.png', 7, 124),
    (125, N'Pincho', N'https://piuimages.arroweclip.se/avatars/p2/759bb6007aa0a3a771c4424f1151a0a6.png', 6, 125),
    (126, N'PIU-NX-01 Phenix', N'https://piuimages.arroweclip.se/avatars/p2/4c8827d805d35224ee1651ded58c24e1.png', 6, 126),
    (127, N'PIU-NX-02 Lapin', N'https://piuimages.arroweclip.se/avatars/p2/9113471082ffeb962d01752f3c1ea7f8.png', 6, 127),
    (128, N'Princess Choco & Candy Girl', N'https://piuimages.arroweclip.se/avatars/p2/ccf1e801a6b3b3ac591c8590d60db263.png', 7, 128),
    (129, N'Pumcaddy', N'https://piuimages.arroweclip.se/avatars/xx/153.png', 1, 129),
    (130, N'Rae A', N'https://piuimages.arroweclip.se/avatars/p2/3dbd4b75cc14a338e2253a708c77a5b8.png', 7, 130),
    (131, N'Rakha', N'https://piuimages.arroweclip.se/avatars/p2/ddb083f04b880856a0345904272b63d6.png', 6, 131),
    (132, N'Researcher', N'https://piuimages.arroweclip.se/avatars/p2/39ee85e4119925edff462559b97dc54c.png', 6, 132),
    (133, N'Revy', N'https://piuimages.arroweclip.se/avatars/p2/7c44a40f70400bccfcb78b9f9c0106cf.png', 6, 133),
    (134, N'Rhyth', N'https://piuimages.arroweclip.se/avatars/p2/943b70763e08c39475d5438384ea2bf4.png', 7, 134),
    (135, N'Rio & Rou', N'https://piuimages.arroweclip.se/avatars/p2/e87fe3d25615e9b514dfb2727f73df1e.png', 7, 135),
    (136, N'RIP', N'https://piuimages.arroweclip.se/avatars/p2/0928474b9770d80cc5d1669bd0f3259b.png', 6, 136),
    (137, N'Robber Rider', N'https://piuimages.arroweclip.se/avatars/p2/62d107b0127ce4a03909575ff1823bd3.png', 6, 137),
    (138, N'Saimon', N'https://piuimages.arroweclip.se/avatars/p2/72df055536cc5fa3edf706d83a38f82b.png', 6, 138),
    (139, N'Sarabande', N'https://piuimages.arroweclip.se/avatars/p2/7a0f025f362dd2153172f4a2e10b57f9.png', 7, 139),
    (140, N'Saya', N'https://piuimages.arroweclip.se/avatars/p2/739d0710f8115b5bdb0b4c92175ff6a5.png', 6, 140),
    (141, N'Sento Yamada', N'https://piuimages.arroweclip.se/avatars/p2/d412300fc63b1b72d65d4b51606d827d.png', 6, 141),
    (142, N'Seulki', N'https://piuimages.arroweclip.se/avatars/p2/67217161e1a00f1cea4ac9e702cce061.png', 7, 142),
    (143, N'Silvia', N'https://piuimages.arroweclip.se/avatars/p2/6ac06e7ec80e7f360f340d6bcadcd2bc.png', 6, 143),
    (144, N'Sister''s Death', N'https://piuimages.arroweclip.se/avatars/p2/7d4dcf81315460c5012e8bbe21ec0066.png', 6, 144),
    (145, N'Sonic boom protocol', N'https://piuimages.arroweclip.se/avatars/p2/c66dcf2d509b2fccab8896b3c21f0579.png', 6, 145),
    (146, N'Stella', N'https://piuimages.arroweclip.se/avatars/p2/bf1cbf325b3468dd652b37cefb8394e3.png', 6, 146),
    (147, N'Stranger', N'https://piuimages.arroweclip.se/avatars/p2/81bef6381fab9640e6b4975ac28f10c0.png', 7, 147),
    (148, N'Successor of Lore', N'https://piuimages.arroweclip.se/avatars/p2/c71a2b2037c95c604208db7cc94ca9ef.png', 6, 148),
    (149, N'Super Akuma Emperor', N'https://piuimages.arroweclip.se/avatars/p2/eede9451b5f58ff561d7f0ccc269cb84.png', 6, 149),
    (150, N'Syndi', N'https://piuimages.arroweclip.se/avatars/p2/6ad16ac7c91e1fab89d4d31fcee8be65.png', 6, 150),
    (151, N'Synt. H. Wulf', N'https://piuimages.arroweclip.se/avatars/p2/9a54da27d2667430d57096bc61e80d91.png', 7, 151),
    (152, N'Taeho', N'https://piuimages.arroweclip.se/avatars/p2/3efae2202bc24e85c22bf1280801f982.png', 7, 152),
    (153, N'Takalook', N'https://piuimages.arroweclip.se/avatars/p2/02ec651ce5550094cbfc3c0f6624d667.png', 7, 153),
    (154, N'TEDDY', N'https://piuimages.arroweclip.se/avatars/p2/5ac5db4aa3aaeb7320eb7c2eb687b37a.png', 6, 154),
    (155, N'The Crawling Chaos', N'https://piuimages.arroweclip.se/avatars/p2/edd6b161a7bff3f240aa3049c4f57367.png', 7, 155),
    (156, N'The Cutie', N'https://piuimages.arroweclip.se/avatars/p2/79b22cb277ede285557000e5aa8ef670.png', 6, 156),
    (157, N'The Grim Reaper', N'https://piuimages.arroweclip.se/avatars/p2/e9fc73f96b327c5611ff8c80fd06b9a5.png', 7, 157),
    (158, N'The Harbinger', N'https://piuimages.arroweclip.se/avatars/p2/2b37a1ac5c881384cb1f79d94ec20185.png', 6, 158),
    (159, N'The Quick Brown Fox', N'https://piuimages.arroweclip.se/avatars/p2/3471d6874d5ec26b2e334ab9b60ee2b6.png', 7, 159),
    (160, N'The Truth Faced', N'https://piuimages.arroweclip.se/avatars/p2/97a2dc0bb5a4702a0f11ce879407d657.png', 6, 160),
    (161, N'Tina', N'https://piuimages.arroweclip.se/avatars/p2/96cd716cbfe11087778f655ff299c472.png', 6, 161),
    (162, N'Tritium', N'https://piuimages.arroweclip.se/avatars/p2/e0d5b798263e278dd487795e175cd838.png', 7, 162),
    (163, N'Unique Duo', N'https://piuimages.arroweclip.se/avatars/p2/be13a36baf0d31974b5324cd8637383b.png', 7, 163),
    (164, N'Unknown Alchemist', N'https://piuimages.arroweclip.se/avatars/p2/b8096be8f0d76986739bfe1256046e48.png', 6, 164),
    (165, N'Vermilion & Azure', N'https://piuimages.arroweclip.se/avatars/p2/9516a7cc69a1b2b86c6a3541283ca495.png', 7, 165),
    (166, N'Violet Perfume', N'https://piuimages.arroweclip.se/avatars/p2/1c4a2cb7f5945470b611e700cfc86fa5.png', 7, 166),
    (167, N'WHANY', N'https://piuimages.arroweclip.se/avatars/p2/420b610d3688cd7d3335fe59e1a1ce15.png', 6, 167),
    (168, N'Wonder & World', N'https://piuimages.arroweclip.se/avatars/p2/617a39ab8b600e929af6b9dda48b7581.png', 7, 168),
    (169, N'Yeong Dong', N'https://piuimages.arroweclip.se/avatars/p2/725c122975eb058fde280919f181e456.png', 7, 169),
    (170, N'Youyuu Sumire', N'https://piuimages.arroweclip.se/avatars/p2/32136145f509e1ed81ff067c00b4b034.png', 7, 170);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Avatar",
                schema: "scores");
        }
    }
}
