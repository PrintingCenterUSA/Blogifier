if not exists (select * from sys.all_columns where name='ParentId' and object_id=(select object_id from sys.objects where name='blogposts'))
	alter table help.blogposts add ParentId int null

if not exists (select * from sys.all_columns where name='DisplayOrder' and object_id=(select object_id from sys.objects where name='blogposts'))
	alter table help.blogposts add DisplayOrder int default 0

if not exists (select * from sys.all_columns where name='IdPath' and object_id=(select object_id from sys.objects where name='blogposts'))
	alter table help.blogposts add IdPath nvarchar(256) default '/'

update help.blogposts set DisplayOrder=0 where DisplayOrder is null

if not exists(select * from sys.all_columns where name='NavHeader' and OBJECT_ID= 
(select OBJECT_ID from sys.objects where name='BlogPosts' and type='U'))
	alter table help.BlogPosts add NavHeader nvarchar(80)