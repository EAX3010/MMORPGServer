﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace MMORPGServer.Infrastructure.Migrations
{
    [DbContext(typeof(GameDbContext))]
    partial class GameDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "9.0.6")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("MMORPGServer.Infrastructure.Persistence.Models.PlayerEntity", b =>
                {
                    b.Property<int>("Id")
                        .HasColumnType("int");

                    b.Property<short>("Agility")
                        .HasColumnType("smallint");

                    b.Property<int>("BoundConquerPoints")
                        .HasColumnType("int");

                    b.Property<int>("ConquerPoints")
                        .HasColumnType("int");

                    b.Property<DateTime>("CreatedAt")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("datetime2")
                        .HasDefaultValueSql("GETUTCDATE()");

                    b.Property<int>("CurrentHealth")
                        .HasColumnType("int");

                    b.Property<int>("CurrentMana")
                        .HasColumnType("int");

                    b.Property<DateTime?>("DeletedAt")
                        .HasColumnType("datetime2");

                    b.Property<long>("Experience")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint")
                        .HasDefaultValue(0L);

                    b.Property<long>("Gold")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint")
                        .HasDefaultValue(0L)
                        .HasColumnName("Gold");

                    b.Property<bool>("IsDeleted")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<DateTime?>("LastLogin")
                        .HasColumnType("datetime2");

                    b.Property<DateTime?>("LastLogout")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("LastModifiedAt")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("datetime2")
                        .HasDefaultValueSql("GETUTCDATE()");

                    b.Property<int>("Level")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasDefaultValue(1);

                    b.Property<short>("MapId")
                        .HasColumnType("smallint");

                    b.Property<int>("MaxHealth")
                        .HasColumnType("int");

                    b.Property<int>("MaxMana")
                        .HasColumnType("int");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(15)
                        .HasColumnType("nvarchar(15)")
                        .UseCollation("LATIN1_GENERAL_100_CI_AS_SC_UTF8");

                    b.Property<byte[]>("RowVersion")
                        .IsConcurrencyToken()
                        .IsRequired()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("rowversion");

                    b.Property<short>("Spirit")
                        .HasColumnType("smallint");

                    b.Property<short>("Strength")
                        .HasColumnType("smallint");

                    b.Property<short>("Vitality")
                        .HasColumnType("smallint");

                    b.Property<short>("X")
                        .HasColumnType("smallint");

                    b.Property<short>("Y")
                        .HasColumnType("smallint");

                    b.HasKey("Id");

                    b.HasIndex("IsDeleted")
                        .HasDatabaseName("IX_Players_IsDeleted");

                    b.HasIndex("LastLogin")
                        .HasDatabaseName("IX_Players_LastLogin");

                    b.HasIndex("Name")
                        .IsUnique()
                        .HasDatabaseName("IX_Players_Name");

                    b.ToTable("Players", (string)null);
                });
#pragma warning restore 612, 618
        }
    }
}
