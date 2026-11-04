alter table public.master_resumes
  alter column user_id set default auth.uid();
