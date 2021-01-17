namespace MechSym.ControlFlow

module Operators =
    let (>>=) a b = Result.bind b a
    
    let (>=>) (a: 'a -> Result<'b, 'c>) (b: 'b -> Result<'d, 'c>): 'a -> Result<'d, 'c> =
        fun inp ->
            a inp
            |> Result.bind b
        

module Result =
    let iter (op: 'a -> unit) (res: Result<'a, 'b>): unit =
        match res with
        | Ok foo -> op foo
        | Error _x -> ()

    let tee (op: 'a -> unit) (res: Result<'a, 'b>): Result<'a, 'b> =
        match res with
        | Ok foo -> op foo
        | Error _x -> ()   
        res
        
    let raiseOnError (op: 'b -> string) (result: Result<'a, 'b>): unit =
        match result with
        | Ok _ -> ()
        | Error err -> raise (exn (op err))
        
        
    let chain (originalInput: Result<'a, 'b> list): Result<'a list, 'b> =
        
        let rec loop (input: Result<'a, 'b> list) (acc: 'a list): Result<'a list, 'b> =
            match input with
            | head :: tail ->
                match head with
                | Ok result ->
                    loop tail (result :: acc)
                | Error err ->
                    Error err
            | [] ->
                Ok (acc |> List.rev)
                
        loop originalInput []
        
    let delayChain (originalInput: (unit -> Result<'a, 'b>) list): Result<'a list, 'b> =
        
        let rec loop (input: (unit -> Result<'a, 'b>) list) (acc: 'a list): Result<'a list, 'b> =
            match input with
            | head :: tail ->
                match head() with
                | Ok result ->
                    loop tail (result :: acc)
                | Error err ->
                    Error err
            | [] ->
                Ok (acc |> List.rev)
                
        loop originalInput []
        
    let ignore (originalInput: Result<'a, 'b>): Result<unit, 'b> =
        match originalInput with
        | Ok _ -> Ok ()
        | Error err -> Error err
        

