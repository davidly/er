using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

class EditedRaws
{
    static void Usage()
    {
        Console.WriteLine( @"Usage: er <sourcepath> <destinationpath>" );
        Console.WriteLine( @"Edited Raw file copy app" );
        Console.WriteLine( @"  example: er c:\users\david\pictures x:\backup\pictures" );
        Console.WriteLine( @"           er d:\ x:\backup\pictures" );
        Console.WriteLine( @"  notes:" );
        Console.WriteLine( @"    This app recursively looks for lightroom .xmp files then copies those and the raw images they reference." );
        Console.WriteLine( @"    This enables backup of images you care about (and have edited) vs. those not edited." );
        Console.WriteLine( @"    .dng RAW files (Ricoh, Leica, etc.) store Lightroom data. They have no .xmp. Backup these separately." );
        Console.WriteLine( @"    Same for .jpg, .tif, .tiff. -- Lightroom stores edits in the files, not separate .xmp files." );
        Environment.Exit( 1 );
    } //Usage

    static bool CopyImageFile( string fullPath, string srcRoot, string dstRoot )
    {
        bool copied = false;

        try
        {
            string name = Path.GetFileName( fullPath );
    
            string dstDirectory = Path.GetDirectoryName( fullPath ).Substring( srcRoot.Length );
            dstDirectory = dstRoot + dstDirectory;
            string dstPath = Path.Combine( dstDirectory, name );
    
            DateTime dtSrc = File.GetLastWriteTimeUtc( fullPath );
            DateTime dtDst = File.GetLastWriteTimeUtc( dstPath );
    
            if ( 0 != DateTime.Compare( dtSrc, dtDst ) )
            {
                Console.WriteLine( "copying from {0} to {1}", fullPath, dstPath );

                Directory.CreateDirectory( dstDirectory );
                File.Copy( fullPath, dstPath, true );
                copied = true;
            }
        }
        catch ( Exception ex)
        {
            Console.WriteLine( "exception {0} caught copying {1}", ex.ToString(), fullPath );
        }

        return copied;
    } //CopyImageFile

    static void Main( string[] args )
    {
        Stopwatch stopWatch = new Stopwatch();
        stopWatch.Start();

        if ( 2 != args.Length )
            Usage();

        string extension = @"*.xmp";
        string srcRoot = args[ 0 ];
        string dstRoot = args[ 1 ];

        srcRoot = Path.GetFullPath( srcRoot );
        dstRoot = Path.GetFullPath( dstRoot );

        Console.WriteLine( "Copying from {0} to {1}", srcRoot, dstRoot );

        object objLock = new object();
        long bytesCopied = 0;
        long xmpsCopied = 0;
        long xmpsExamined = 0;
        long filesCopied = 0;

        // foreach ( FileInfo fi in GetFilesInfo( srcRoot, extension ) )

        Parallel.ForEach( GetFilesInfo( srcRoot, extension ), new ParallelOptions { MaxDegreeOfParallelism = 64 }, (fsiXMP) =>
        {
            FileInfo fi = (FileInfo) fsiXMP;
            string fullPath = fi.FullName;
            Interlocked.Increment( ref xmpsExamined );

            try
            {
                DirectoryInfo di = fi.Directory;
                string name = fi.Name;

#if true // Crack the .xmp file and look for the raw file name. Only works for Adobe-generated .xmp files.

                // Two patterns supported:
                //    crs:RawFileName="P1034659.RW2">
                //    <crs:RawFileName>P1000458.RW2</crs:RawFileName>

                string xmpText = File.ReadAllText( fullPath );
                const string crsTAG = "crs:RawFileName";

                int rawTag = xmpText.IndexOf( crsTAG, StringComparison.InvariantCultureIgnoreCase );

                if ( -1 == rawTag )
                {
                    Console.WriteLine( "tag crs:RawFileName not found in {0}; skipping it.", fullPath );
                    return; // continue if a normal foreach
                }

                int fileValue = rawTag + crsTAG.Length;
                while ( xmpText[ fileValue ] == '=' ||
                        xmpText[ fileValue ] == '"' ||
                        xmpText[ fileValue ] == '>' )
                    fileValue++;

                int fileLen = 0;
                while ( xmpText[ fileValue + fileLen ] != '"' &&
                        xmpText[ fileValue + fileLen ] != '<' )
                    fileLen++;

                string rawName = xmpText.Substring( fileValue, fileLen );
                string rawFullPath = Path.Combine( di.FullName, rawName );

                if ( CopyImageFile( fullPath, srcRoot, dstRoot ) )
                {
                    Interlocked.Increment( ref xmpsCopied );
                    Interlocked.Increment( ref filesCopied );

                    lock ( objLock )
                        bytesCopied += fi.Length;
                }

                if ( CopyImageFile( rawFullPath, srcRoot, dstRoot ) )
                {
                    Interlocked.Increment( ref filesCopied );

                    try
                    {
                        long bytes = new System.IO.FileInfo( rawFullPath ).Length;

                        lock ( objLock )
                            bytesCopied += bytes;
                    }
                    catch( Exception ex )
                    {
                        Console.WriteLine( "can't query file length for {0}, exception {1}", rawFullPath, ex.ToString() );
                    }
                }

#else // Look for files with the same name and all possible extensions in the same directory, and copy those

                // justName minus ".xmp"

                string justName = name.Substring( 0, name.Length - 4 );
                string rawPattern = justName + ".*";

                FileSystemInfo[] fsi = di.GetFileSystemInfos( rawPattern );

                // The foreach will include the original .xmp file, and that's OK because that should be copied too
                // If the folder has RAW+jpg, copy both since it's not certain which file the .xmp pertains to. Also, jpgs are small.
                // Parallel in outer loop is sufficient, so don't parallel here since that's plenty of parallelism.

                //Parallel.ForEach( fsi, (fsiRaw) =>
                foreach ( FileInfo fsiRaw in fsi )
                {
                    FileInfo fiRaw = (FileInfo) fsiRaw;

                    string ext = Path.GetExtension( fiRaw.Name ).ToLower();
                    string rawSrcPath = fiRaw.FullName;

                    string rawDstDirectory = fiRaw.DirectoryName.Substring( srcRoot.Length );
                    rawDstDirectory = dstRoot + rawDstDirectory;
                    string rawDstPath = Path.Combine( rawDstDirectory, fiRaw.Name );

                    DateTime dtSrc = fiRaw.LastWriteTimeUtc;
                    DateTime dtDst = File.GetLastWriteTimeUtc( rawDstPath );

                    if ( 0 != DateTime.Compare( dtSrc, dtDst ) )
                    {
                        Console.WriteLine( "copying from {0} to {1}", rawSrcPath, rawDstPath );

                        if ( 0 == String.Compare( ext, @".xmp" ) )
                            xmpsCopied++;

                        Directory.CreateDirectory( rawDstDirectory );

                        bytesCopied += fiRaw.Length;
                        filesCopied++;

                        File.Copy( rawSrcPath, rawDstPath, true );
                    }
                    else
                    {
                        Console.WriteLine( "no need to copy from {0} to {1}", rawSrcPath, rawDstPath );
                    }
                } //);
#endif
            }
            catch ( Exception ex )
            {
                Console.WriteLine( "exception {0} caught", ex.ToString() );
                Console.WriteLine( "exception processing (path too long?) " + fullPath );
            }
        });

        Console.WriteLine( "Examined {0} .xmp files. Copied {1} .xmp files and {2} total files consuming {3,0:N0} bytes, which is {4} GB",
                           xmpsExamined, xmpsCopied, filesCopied, bytesCopied, bytesCopied / ( 1024 * 1024 * 1024 ) );

        if ( 0 != bytesCopied && 0 != stopWatch.ElapsedMilliseconds )
        {
            double bytesPerMillisecond = (double) bytesCopied / (double) stopWatch.ElapsedMilliseconds;
            double bytesPerSecond = bytesPerMillisecond * 1000.0;

            Console.WriteLine( "Copy rate {0:0.##} MB/sec", bytesPerSecond / (double) ( 1024 * 1024 ) );
        }
    } //Main

    static IEnumerable<FileInfo> GetFilesInfo( string path, string extension )
    {
        Queue<string> queue = new Queue<string>();
        queue.Enqueue( path );
    
        while ( queue.Count > 0 )
        {
            path = queue.Dequeue();
            try
            {
                // GetDirectories will not return any subdirectories under a reparse point

                foreach ( string subDir in Directory.GetDirectories( path ) )
                    queue.Enqueue( subDir );
            }
            catch(Exception ex)
            {
                Console.Error.WriteLine( "Exception finding subdirectories {0} in {1}", ex.ToString(), path );
            }

            FileInfo[] files = null;
            try
            {
                DirectoryInfo di = new DirectoryInfo( path );
                files = di.GetFiles(  extension );
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine( "Exception finding .xmp files {0} in {1}", ex.ToString(), path );
            }
    
            if ( files != null )
            {
                for ( int i = 0 ; i < files.Length; i++ )
                {
                    yield return files[ i ];
                }
            }
        }
    } //GetFilesInfo
} //EditedRaws
