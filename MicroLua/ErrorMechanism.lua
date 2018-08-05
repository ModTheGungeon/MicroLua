--MicroLua--

-- This file is based on Eluant source code (BindingSupport.lua)

-- It enables safe calling of C# methods from Lua, as it avoids the use of
-- lua_error which triggers a longjmp(). Raising errors is done
-- entirely while in Lua, by creating a wrapper function around the
-- actual function that will in the end receive either one or two
-- flags - the first one from pcall, and the second one from the
-- C# side of the mechanism that will return 'false' and the exception
-- error message if one is catched.

-- The microlua_make_call_wrapper function is later acquired in C#
-- and removed from globals, so it does not clutter up the namespace.


local _pcall = pcall
local _error = error
local _select = select

local function microlua_process_call_result(pcall_flag, clr_flag, ...)
   if not pcall_flag then _error(clr_flag) end
   -- if pcall fails, clr_flag is actually the error message

   if not clr_flag then _error(_select(1, ...)) end
   -- we pick only the first argument to avoid a case where
   -- more than one would be returned and error would receive
   -- a bogus second argument

   return ...
end

function microlua_make_call_wrapper(fn)
    return function(...)
        return microlua_process_call_result(_pcall(fn, ...))
    end
end