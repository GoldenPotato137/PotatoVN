﻿// <auto-generated />
using System;
using GalgameManager.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace GalgameManager.Core.Migrations
{
    [DbContext(typeof(DataContext))]
    [Migration("20240105062100_Init")]
    partial class Init
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "7.0.14");

            modelBuilder.Entity("CategoryGalgame", b =>
                {
                    b.Property<int>("CategoriesId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("GalgamesId")
                        .HasColumnType("INTEGER");

                    b.HasKey("CategoriesId", "GalgamesId");

                    b.HasIndex("GalgamesId");

                    b.ToTable("CategoryGalgame");
                });

            modelBuilder.Entity("GalgameManager.Core.Models.Category", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.ToTable("Categories");
                });

            modelBuilder.Entity("GalgameManager.Core.Models.GalTag", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int>("GalgameId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Tag")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("GalgameId");

                    b.ToTable("GalTag");
                });

            modelBuilder.Entity("GalgameManager.Core.Models.Galgame", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("BgmId")
                        .HasColumnType("TEXT");

                    b.Property<string>("CnName")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Comment")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Developer")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("ExePath")
                        .HasColumnType("TEXT");

                    b.Property<string>("ExpectedPlayTime")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("ImagePath")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("ImageUrl")
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("LastPlay")
                        .HasColumnType("TEXT");

                    b.Property<string>("MixedId")
                        .HasColumnType("TEXT");

                    b.Property<int>("MyRate")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Path")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<int>("PlayType")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("PrivateComment")
                        .HasColumnType("INTEGER");

                    b.Property<string>("ProcessName")
                        .HasColumnType("TEXT");

                    b.Property<float>("Rating")
                        .HasColumnType("REAL");

                    b.Property<DateTime>("ReleaseDate")
                        .HasColumnType("TEXT");

                    b.Property<int>("RssType")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("RunAsAdmin")
                        .HasColumnType("INTEGER");

                    b.Property<string>("SavePath")
                        .HasColumnType("TEXT");

                    b.Property<int>("TotalPlayTime")
                        .HasColumnType("INTEGER");

                    b.Property<string>("VndbId")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Galgames");
                });

            modelBuilder.Entity("GalgameManager.Core.Models.PlayLog", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("Date")
                        .HasColumnType("TEXT");

                    b.Property<int>("GalgameId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Minute")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("GalgameId");

                    b.ToTable("PlayLog");
                });

            modelBuilder.Entity("CategoryGalgame", b =>
                {
                    b.HasOne("GalgameManager.Core.Models.Category", null)
                        .WithMany()
                        .HasForeignKey("CategoriesId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("GalgameManager.Core.Models.Galgame", null)
                        .WithMany()
                        .HasForeignKey("GalgamesId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("GalgameManager.Core.Models.GalTag", b =>
                {
                    b.HasOne("GalgameManager.Core.Models.Galgame", "Galgame")
                        .WithMany("Tags")
                        .HasForeignKey("GalgameId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Galgame");
                });

            modelBuilder.Entity("GalgameManager.Core.Models.PlayLog", b =>
                {
                    b.HasOne("GalgameManager.Core.Models.Galgame", "Galgame")
                        .WithMany("PlayTime")
                        .HasForeignKey("GalgameId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Galgame");
                });

            modelBuilder.Entity("GalgameManager.Core.Models.Galgame", b =>
                {
                    b.Navigation("PlayTime");

                    b.Navigation("Tags");
                });
#pragma warning restore 612, 618
        }
    }
}
