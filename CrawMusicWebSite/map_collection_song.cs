//------------------------------------------------------------------------------
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
    using System.Collections.Generic;
    
    public partial class map_collection_song
    {
        public int CollectionId { get; set; }
        public string songId { get; set; }
        public int Id { get; set; }
    
        public virtual Collection Collection { get; set; }
    }
}
