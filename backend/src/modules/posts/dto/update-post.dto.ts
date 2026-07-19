import { PartialType } from '@nestjs/mapped-types';
import { PostData } from './create-post.dto';

export class UpdatePostData extends PartialType(PostData) { }
