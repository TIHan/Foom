open Microsoft.FSharp.Compiler.Interactive    
open Microsoft.FSharp.Compiler.Interactive.Settings    

open System.Threading.Tasks

let Create () =            
    let invoke = ref (new Task (fun () -> ()))
    { new IEventLoop with             
        member x.Run () =                  
            Program.start invoke               
            false         

        member x.Invoke(f) =                  
            try 
                let mutable result = Unchecked.defaultof<'a>

                invoke := new Task (fun () ->
                    result <- f ()
                )

                (!invoke).Wait ()
                result

            with e -> eprintf "\n\n ERROR: %O\n" e; reraise()             

        member x.ScheduleRestart () = ()                       
     }     
    
fsi.EventLoop <-  Create()
