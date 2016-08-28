[<RequireQualifiedAccess>] 
module Foom.Math.Math

// http://stackoverflow.com/questions/3407012/c-rounding-up-to-the-nearest-multiple-of-a-number
let inline roundUp numToRound multiple =
    if (multiple = 0) then 
        numToRound
    else
        let remainder = abs (numToRound) % multiple;
        if (remainder = 0) then 
            numToRound
        else

            if (numToRound < 0) then
                -(abs (numToRound) - remainder)
            else
                numToRound + multiple - remainder

let inline roundDown numToRound multiple =
    roundUp numToRound multiple - multiple