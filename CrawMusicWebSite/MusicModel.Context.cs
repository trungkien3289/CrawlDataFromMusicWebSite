﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace CrawMusicWebSite
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Infrastructure;
    
    public partial class MusicStoreEntities : DbContext
    {
        public MusicStoreEntities()
            : base("name=MusicStoreEntities")
        {
        }
    
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            throw new UnintentionalCodeFirstException();
        }
    
        public virtual DbSet<ms_Album> ms_Album { get; set; }
        public virtual DbSet<ms_Artist> ms_Artist { get; set; }
        public virtual DbSet<ms_Collection> ms_Collection { get; set; }
        public virtual DbSet<system_RouteData> system_RouteData { get; set; }
        public virtual DbSet<ms_Genre> ms_Genre { get; set; }
        public virtual DbSet<ms_Song> ms_Song { get; set; }
    }
}
