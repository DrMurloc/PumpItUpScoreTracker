﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ScoreTracker.Data.Persistence;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    [DbContext(typeof(ChartAttemptDbContext))]
    [Migration("20230917233627_AddUserTourneyEntry")]
    partial class AddUserTourneyEntry
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasDefaultSchema("scores")
                .HasAnnotation("ProductVersion", "6.0.4")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder, 1L, 1);

            modelBuilder.Entity("ScoreTracker.Data.Persistence.Entities.BestAttemptEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("ChartId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<bool>("IsBroken")
                        .HasColumnType("bit");

                    b.Property<string>("LetterGrade")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTimeOffset>("RecordedDate")
                        .HasColumnType("datetimeoffset");

                    b.Property<int?>("Score")
                        .HasColumnType("int");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("ChartId");

                    b.HasIndex("UserId", "ChartId");

                    b.ToTable("BestAttempt", "scores");
                });

            modelBuilder.Entity("ScoreTracker.Data.Persistence.Entities.ChartDifficultyRatingEntity", b =>
                {
                    b.Property<Guid>("ChartId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("MixId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<int>("Count")
                        .HasColumnType("int");

                    b.Property<double>("Difficulty")
                        .HasColumnType("float");

                    b.Property<double>("StandardDeviation")
                        .HasColumnType("float");

                    b.HasKey("ChartId", "MixId");

                    b.ToTable("ChartDifficultyRating", "scores");
                });

            modelBuilder.Entity("ScoreTracker.Data.Persistence.Entities.ChartEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid?>("DifficultyRatingChartId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid?>("DifficultyRatingMixId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<int>("Level")
                        .HasColumnType("int");

                    b.Property<Guid>("SongId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.HasKey("Id");

                    b.HasIndex("Level");

                    b.HasIndex("SongId");

                    b.HasIndex("Type");

                    b.HasIndex("DifficultyRatingChartId", "DifficultyRatingMixId");

                    b.ToTable("Chart", "scores");
                });

            modelBuilder.Entity("ScoreTracker.Data.Persistence.Entities.ChartMixEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("ChartId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<int>("Level")
                        .HasColumnType("int");

                    b.Property<Guid>("MixId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("ChartId");

                    b.HasIndex("Level");

                    b.HasIndex("MixId");

                    b.ToTable("ChartMix", "scores");
                });

            modelBuilder.Entity("ScoreTracker.Data.Persistence.Entities.ChartVideoEntity", b =>
                {
                    b.Property<Guid>("ChartId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("ChannelName")
                        .IsRequired()
                        .HasMaxLength(30)
                        .HasColumnType("nvarchar(30)");

                    b.Property<string>("VideoUrl")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("nvarchar(64)");

                    b.HasKey("ChartId");

                    b.ToTable("ChartVideo", "scores");
                });

            modelBuilder.Entity("ScoreTracker.Data.Persistence.Entities.CoOpRatingEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("ChartId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<int>("Difficulty")
                        .HasColumnType("int");

                    b.Property<int>("Player")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("ChartId");

                    b.ToTable("CoOpRating", "scores");
                });

            modelBuilder.Entity("ScoreTracker.Data.Persistence.Entities.ExternalLoginEntity", b =>
                {
                    b.Property<string>("LoginProvider")
                        .HasMaxLength(32)
                        .HasColumnType("nvarchar(32)");

                    b.Property<string>("ExternalId")
                        .HasMaxLength(64)
                        .HasColumnType("nvarchar(64)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("LoginProvider", "ExternalId");

                    b.HasIndex("UserId");

                    b.ToTable("ExternalLogin", "scores");
                });

            modelBuilder.Entity("ScoreTracker.Data.Persistence.Entities.MixEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(7)
                        .HasColumnType("nvarchar(7)");

                    b.HasKey("Id");

                    b.ToTable("Mix", "scores");
                });

            modelBuilder.Entity("ScoreTracker.Data.Persistence.Entities.PhoenixRecordEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("ChartId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<bool>("IsBroken")
                        .HasColumnType("bit");

                    b.Property<string>("LetterGrade")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Plate")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTimeOffset>("RecordedDate")
                        .HasColumnType("datetimeoffset");

                    b.Property<int?>("Score")
                        .HasColumnType("int");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("ChartId");

                    b.HasIndex("UserId");

                    b.ToTable("PhoenixRecord", "scores");
                });

            modelBuilder.Entity("ScoreTracker.Data.Persistence.Entities.SavedChartEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("ChartId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("ListName")
                        .IsRequired()
                        .HasMaxLength(16)
                        .HasColumnType("nvarchar(16)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("ChartId");

                    b.HasIndex("ListName");

                    b.HasIndex("UserId");

                    b.ToTable("SavedChart", "scores");
                });

            modelBuilder.Entity("ScoreTracker.Data.Persistence.Entities.SongEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<long>("Duration")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint")
                        .HasDefaultValue(0L);

                    b.Property<string>("ImagePath")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("Type")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(8)
                        .HasColumnType("nvarchar(8)")
                        .HasDefaultValue("Arcade");

                    b.HasKey("Id");

                    b.HasIndex("Name");

                    b.ToTable("Song", "scores");
                });

            modelBuilder.Entity("ScoreTracker.Data.Persistence.Entities.TournamentEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Configuration")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTimeOffset?>("EndDate")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTimeOffset?>("StartDate")
                        .HasColumnType("datetimeoffset");

                    b.HasKey("Id");

                    b.ToTable("Tournament", "scores");
                });

            modelBuilder.Entity("ScoreTracker.Data.Persistence.Entities.UserChartDifficultyRatingEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("ChartId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("MixId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<int>("Scale")
                        .HasColumnType("int");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("ChartId");

                    b.HasIndex("UserId", "ChartId");

                    b.ToTable("UserChartDifficultyRating", "scores");
                });

            modelBuilder.Entity("ScoreTracker.Data.Persistence.Entities.UserCoOpRatingEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("ChartId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<int>("Difficulty")
                        .HasColumnType("int");

                    b.Property<int>("Player")
                        .HasColumnType("int");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("ChartId");

                    b.HasIndex("UserId");

                    b.ToTable("UserCoOpRating", "scores");
                });

            modelBuilder.Entity("ScoreTracker.Data.Persistence.Entities.UserEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<bool>("IsPublic")
                        .HasColumnType("bit");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.HasKey("Id");

                    b.ToTable("User", "scores");
                });

            modelBuilder.Entity("ScoreTracker.Data.Persistence.Entities.UserSettingsEntity", b =>
                {
                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("UiSettings")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("UserId");

                    b.ToTable("UserSettings", "scores");
                });

            modelBuilder.Entity("ScoreTracker.Data.Persistence.Entities.UserTournamentSessionEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("ChartEntries")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("SessionScore")
                        .HasColumnType("int");

                    b.Property<Guid>("TournamentId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("TournamentId", "UserId");

                    b.ToTable("UserTournamentSession", "scores");
                });

            modelBuilder.Entity("ScoreTracker.Data.Persistence.Entities.BestAttemptEntity", b =>
                {
                    b.HasOne("ScoreTracker.Data.Persistence.Entities.ChartEntity", null)
                        .WithMany()
                        .HasForeignKey("ChartId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("ScoreTracker.Data.Persistence.Entities.UserEntity", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("ScoreTracker.Data.Persistence.Entities.ChartEntity", b =>
                {
                    b.HasOne("ScoreTracker.Data.Persistence.Entities.SongEntity", null)
                        .WithMany()
                        .HasForeignKey("SongId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("ScoreTracker.Data.Persistence.Entities.ChartDifficultyRatingEntity", "DifficultyRating")
                        .WithMany()
                        .HasForeignKey("DifficultyRatingChartId", "DifficultyRatingMixId");

                    b.Navigation("DifficultyRating");
                });

            modelBuilder.Entity("ScoreTracker.Data.Persistence.Entities.ChartMixEntity", b =>
                {
                    b.HasOne("ScoreTracker.Data.Persistence.Entities.ChartEntity", null)
                        .WithMany()
                        .HasForeignKey("ChartId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("ScoreTracker.Data.Persistence.Entities.MixEntity", null)
                        .WithMany()
                        .HasForeignKey("MixId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("ScoreTracker.Data.Persistence.Entities.ChartVideoEntity", b =>
                {
                    b.HasOne("ScoreTracker.Data.Persistence.Entities.ChartEntity", null)
                        .WithMany()
                        .HasForeignKey("ChartId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("ScoreTracker.Data.Persistence.Entities.CoOpRatingEntity", b =>
                {
                    b.HasOne("ScoreTracker.Data.Persistence.Entities.ChartEntity", null)
                        .WithMany()
                        .HasForeignKey("ChartId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("ScoreTracker.Data.Persistence.Entities.ExternalLoginEntity", b =>
                {
                    b.HasOne("ScoreTracker.Data.Persistence.Entities.UserEntity", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("ScoreTracker.Data.Persistence.Entities.PhoenixRecordEntity", b =>
                {
                    b.HasOne("ScoreTracker.Data.Persistence.Entities.ChartEntity", null)
                        .WithMany()
                        .HasForeignKey("ChartId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("ScoreTracker.Data.Persistence.Entities.UserEntity", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("ScoreTracker.Data.Persistence.Entities.SavedChartEntity", b =>
                {
                    b.HasOne("ScoreTracker.Data.Persistence.Entities.ChartEntity", null)
                        .WithMany()
                        .HasForeignKey("ChartId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("ScoreTracker.Data.Persistence.Entities.UserEntity", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("ScoreTracker.Data.Persistence.Entities.UserChartDifficultyRatingEntity", b =>
                {
                    b.HasOne("ScoreTracker.Data.Persistence.Entities.ChartEntity", null)
                        .WithMany()
                        .HasForeignKey("ChartId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("ScoreTracker.Data.Persistence.Entities.UserEntity", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("ScoreTracker.Data.Persistence.Entities.UserCoOpRatingEntity", b =>
                {
                    b.HasOne("ScoreTracker.Data.Persistence.Entities.ChartEntity", null)
                        .WithMany()
                        .HasForeignKey("ChartId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("ScoreTracker.Data.Persistence.Entities.UserEntity", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("ScoreTracker.Data.Persistence.Entities.UserSettingsEntity", b =>
                {
                    b.HasOne("ScoreTracker.Data.Persistence.Entities.UserEntity", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });
#pragma warning restore 612, 618
        }
    }
}