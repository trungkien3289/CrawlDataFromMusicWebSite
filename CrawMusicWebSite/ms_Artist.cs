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
    
    public partial class ms_Artist
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public ms_Artist()
        {
            this.ms_Album = new HashSet<ms_Album>();
            this.ms_Song = new HashSet<ms_Song>();
        }
    
        public int Id { get; set; }
        public string Name { get; set; }
        public Nullable<int> Status { get; set; }
        public string Url { get; set; }
        public string Thumbnail { get; set; }
    
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<ms_Album> ms_Album { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<ms_Song> ms_Song { get; set; }
    }
}